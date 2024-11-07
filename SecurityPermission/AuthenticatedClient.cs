using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Security;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SecurityPermission
{
    /// <summary>
    /// This class can perform login to XProtect using basic, Windows and DefaultWindows credentials
    /// 
    /// Resulting token is an OAuth 2.0 token
    /// 
    /// Refresh token and timeout values are available, but refresh of token is not implemented
    /// </summary>
    internal class AuthenticatedClient
    {
        private static string ClientIdString = "VmsAdminClient";       // Administrators should use: VmsAdminClient
        private string _access_token = null;

        private HttpClient _client;
        private UriHelper _uriHelper;

        /// <summary>
        /// IDP Authentication error_codes:
        /// </summary>
        public const string ChangePassword = "ChangePassword";
        public const string LockedOut = "LockedOut";
        public const string InvalidCredentials = "InvalidCredentials";

        public TokenResponse _tokenResponse;

        internal AuthenticatedClient(UriHelper uriHelper)
        {
            _uriHelper = uriHelper;
        }

        #region login handling
        /// <summary>
        /// Try to login as a basic user, and save the access_token for next calls
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <exception cref="SecurityException">When logon was not successful</exception>
        internal void TryLoginBasic(string username, string password)
        {
            _client = new HttpClient();
            BuildHeaders(_client);

            var postValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("grant_type", "password"),     // Note: Only works for basic users
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("client_id", ClientIdString),
            };

            var request = new HttpRequestMessage(HttpMethod.Post, _uriHelper.IdpConnectUri)
            {
                Content = new FormUrlEncodedContent(postValues)
            };

            var response = SendAndReadContent<TokenResponse>(_client, request);
            try
            {
                _tokenResponse = response.Result;
                _access_token = _tokenResponse.access_Token;
            } catch (Exception ex)  // This is an AggregateException
            {
                throw ex.InnerException;
            }
        }

        internal void TryLoginWindows(ICredentials credentials)
        {
            _client = new HttpClient(CreateClientHandler(credentials));
            BuildHeaders(_client);

            var request = new HttpRequestMessage(HttpMethod.Post, _uriHelper.IdpConnectUri);

            var postValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("grant_type", "windows_credentials"),  
                new KeyValuePair<string, string>("client_id", ClientIdString),
            };

            request.Content = new FormUrlEncodedContent(postValues);

            var response = SendAndReadContent<TokenResponse>(_client, request);
            try
            {
                _tokenResponse = response.Result;
                _access_token = _tokenResponse.access_Token;
            }
            catch (Exception ex)  // This is an AggregateException
            {
                throw ex.InnerException;
            }
        }

        private void BuildHeaders(HttpClient client)
        {
            if (Debugger.IsAttached)
            {
                client.Timeout = TimeSpan.FromMinutes(20);
            }

            client.BaseAddress = _uriHelper.ApiGatewayUri;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private static HttpClientHandler CreateClientHandler(ICredentials credentials = null)
        {
            return new HttpClientHandler()
            {
                Credentials = credentials,
                UseDefaultCredentials = credentials == null ? true : credentials == CredentialCache.DefaultCredentials
            };
        }

        #endregion login handling

        #region HTTP Request handling

        internal T HttpGetData<T>(string uri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _access_token);

            var response = SendAndReceiveData<T>(_client, request);
            return response.Result;
        }

        internal T[] HttpGetArray<T>(string uri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _access_token);

            var response = SendAndReceiveArray<T>(_client, request);
            return response.Result;
        }

        /// <summary>
        /// Helper function which send a request to the IDP
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/></exception>
        /// <exception cref="IdpException">The request was not successful</exception>
        protected Task<T> SendAndReadContent<T>(HttpClient client, HttpRequestMessage request)
        {
            return Send<T>(client, request, true);
        }

        private async Task<T> Send<T>(HttpClient client, HttpRequestMessage request, bool readContent)
        {
            using (var response = await client.SendAsync(request).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    dynamic responseContent = JsonConvert.DeserializeObject<dynamic>(responseString);
                    string error_code = responseContent.error_code;

                    if (error_code == InvalidCredentials)
                        throw new SecurityException(error_code);
                    if (error_code == LockedOut)
                        throw new SecurityException(error_code);
                    if (error_code == ChangePassword)
                        throw new SecurityException(error_code);

                    throw new Exception("HTTP Code: " + response.StatusCode + ", Error_text: " + error_code);
                }

                if (readContent)
                {
                    string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<T>(responseString);
                }

                return default(T);
            }
        }

        /// <summary>
        /// Send request and pick out the "data" part of the response and return as the specified type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<T> SendAndReceiveData<T>(HttpClient client, HttpRequestMessage request)
        {
            using (var response = await client.SendAsync(request).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("HTTP Code: " + response.StatusCode);
                }

                string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseObject = JsonConvert.DeserializeObject<DataResponse<T>>(responseString);
                return responseObject.data;
            }
        }

        /// <summary>
        /// Send request an pick out the "array" part of the reponse, to produce a return value as array of the specified type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<T[]> SendAndReceiveArray<T>(HttpClient client, HttpRequestMessage request)
        {
            using (var response = await client.SendAsync(request).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("HTTP Code: " + response.StatusCode);
                }

                string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseObject = JsonConvert.DeserializeObject<ArrayResponse<T>>(responseString);
                return responseObject.array;
            }
        }

        #endregion
    }

    #region helper classes for deserialization of JSON
    /// <summary>
    /// Used for RestAPI responses containing an array of objects
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ArrayResponse<T>
    {
        public T[] array;
    }

    /// <summary>
    /// Used for RestAPI responses containing a single object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DataResponse<T>
    {
        public T data;
    }

    /// <summary>
    /// Used by IDP response to a token logon attempt
    /// </summary>
    public class TokenResponse
    {
        public string access_Token { get; set; }
        public string refresh_Token { get; set; }
        public string id_token { get; set; }
        public long expires_in { get; set; }
        public string token_type { get; set; }
        public string Scope { get; set; }
    }
    #endregion
}
