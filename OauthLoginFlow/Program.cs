//
// Milestone XProtect SDK Sample Program: OauthLoginFlow
//
// This sample targets developers who wants to use external identity providers for accessing XProtect VMS and API Gateway.
//
// This source code demonstrates how you can login using OIDC and OAuth 2.0 
//
// The sample includes:
//   1 - Get list of well-known URLs, to get the address of the local IDP
//   2 - Find the external provider id, and launch a browser window to let user sign in on external provider
//   3 - Receive a redirected URL from the browser to get a access code
//   4 - Connect to local IDP to convert access code to access token
//   5 - Connect to local VMS to convert access token to VMS Token
//
// The 2 classes should be easy to convert to other languages.
//
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace OauthLoginFlow
{
    class Program
    {
        private const string WellKnownConfigPath = "/IDP/.well-known/openid-configuration";
        private const string AuthorizationUrlKey = "authorization_endpoint";
        private const string ConnectUrlKey = "token_endpoint";
        private const string ExternalProvidersUrlKey = "external_providers_uri";
        private static string ClientIdString = "VmsAdminClient";       // Administrators should use: VmsAdminClient
        private static string Scope = "openid profile managementserver offline_access";
        private const string IdpSignInUrlPath = "/idp/connect/authorize?response_type=code&nonce={0}&state={1}&code_challenge={2}" +
                                "&code_challenge_method=S256&client_id={3}&scope={4}&redirect_uri={5}&acr_values=idp%3A{6}&culture={7}&prompt=login";

        private static string ExternalProviderId = "OIDC";
        private static string AuthorizationUrl = "";
        private static string ConnectUrl = "";
        private static string ExternalProvidersUrl = "";

        private static string serverAddress;

        private static string access_token = null;
        private static string refresh_token = null;
        private static string id_token = null;
        private static long access_token_expires = 3600;                // Is in seconds
        private static DateTime whenToRefreshAccessToken;
        private static string refresh_Secret = "";

        private static string vms_token = null;
        private static long vms_token_expires_in;                       // Is in microseconds
        private static Guid vmsLoginInstanceId = Guid.NewGuid();        // To be used for subsequent re-login (like after 1 hour)
        private static DateTime whenToRefreshVmsToken;

        static void Main(string[] args)
        {
            Console.WriteLine("OAuth 2.0 Login Flow Sample");

            Console.WriteLine("Enter server URL:  (blank is http://localhost)");
            serverAddress = Console.ReadLine();
            if (serverAddress == "")
                serverAddress = "http://localhost";   
            else
                serverAddress = new UriBuilder(serverAddress).Uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);

            try
            {
                ProcessWellKnownConfiguration();
                ProcessExternalProviderList();
                LaunchLoginPage();

                whenToRefreshAccessToken = DateTime.UtcNow + TimeSpan.FromSeconds(access_token_expires);
                Console.WriteLine("Access token expires at: " + whenToRefreshAccessToken.ToLongTimeString());

                ServerCommandServiceLogin(access_token, vmsLoginInstanceId);

                // Calculate when we need to refresh vms_token
                whenToRefreshVmsToken = DateTime.UtcNow + TimeSpan.FromMilliseconds(vms_token_expires_in / 1000);
                Console.WriteLine("VMS token expires at: " + whenToRefreshVmsToken.ToLongTimeString());

                // Now ready to access the VMS
                // Use the access_token for communication to API Gateway and Management Server
                // Use the vms_token for communication to Event Server and Recording Server
                //
                // Note: This will change over time.

                System.Threading.Thread.Sleep(3000);        // Let's assume the refresh time above has expired

                if (ClientIdString != "VmsAdminClient" && refresh_token != null)
                {
                    // When time has passed 'whenToRefreshAccessToken'
                    RefreshAccessToken(ClientIdString, refresh_Secret, refresh_token);
                    whenToRefreshAccessToken = DateTime.UtcNow + TimeSpan.FromSeconds(access_token_expires);
                    Console.WriteLine("Access token expires at: " + whenToRefreshAccessToken.ToLongTimeString());
                }

                // When time has passed 'whenToRefreshVmsToken'
                RefreshVmsToken();
                whenToRefreshVmsToken = DateTime.UtcNow + TimeSpan.FromMilliseconds(vms_token_expires_in / 1000);
                Console.WriteLine("VMS token expires at: " + whenToRefreshVmsToken.ToLongTimeString());

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
            Console.WriteLine("");
            Console.WriteLine("Press any key to end.");
            Console.ReadKey();
        }

        /// <summary>
        /// Construct URL with parameters and launch a browser, that will get redirected to the external providers login page
        /// 
        /// Result of login page is a code, returned in a HTTP redirect to this application via a HTTP listener
        /// 
        /// The code is then used to access local IDP and retrieve an access_token
        /// </summary>
        /// <returns></returns>
        private static void LaunchLoginPage()
        {
            string clientId = Uri.EscapeDataString(ClientIdString);
            string nonce = Guid.NewGuid().ToString("N");
            string state = Utility.GenerateRandom32ByteDataBase64Url();
            string codeVerifier = Utility.GenerateRandom32ByteDataBase64Url();
            string codeChallenge = Utility.GenerateBase64UrlEncodeNoPadding(Utility.Sha256Ascii(codeVerifier));
            string scope = Uri.EscapeDataString(Scope);
            string redirectUri = Utility.CreateHttpListenerEndpoint();
            string redirectUriEncoded = System.Web.HttpUtility.UrlEncode(redirectUri);
            string culture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            string idpSignInUrl = serverAddress + String.Format(CultureInfo.InvariantCulture, IdpSignInUrlPath,
                nonce, state, codeChallenge, clientId, scope, redirectUriEncoded, ExternalProviderId, culture);
            
            string code = RetrieveCodeAsync(redirectUri, idpSignInUrl, state);
            ExchangeCodeForToken(code, codeVerifier, redirectUri, nonce, clientId);
        }

        /// <summary>
        /// Launch browser, and wait for redirect HTTP request to local HTTP listener
        /// </summary>
        /// <param name="redirectUri"></param>
        /// <param name="idpSignInUrl"></param>
        /// <param name="state"></param>
        /// <returns>Code</returns>
        private static string RetrieveCodeAsync(string redirectUri, string idpSignInUrl, string state)
        {
            HttpListener httpListener = null;
            try
            {
                using (httpListener = new HttpListener())
                {
                    httpListener.Prefixes.Add(redirectUri);
                    httpListener.Start();

                    ProcessStartInfo processStartInfo = new ProcessStartInfo(idpSignInUrl);
                    processStartInfo.UseShellExecute = true;
                    Process.Start(processStartInfo);

                    HttpListenerContext context = httpListener.GetContext();

                    string code = context.Request.QueryString["code"];
                    string incomingState = context.Request.QueryString["state"];

                    if (String.IsNullOrEmpty(code))
                    {
                        throw new Exception("No Code received");
                    }

                    if (incomingState != state)
                    {
                        throw new Exception("State values out of sync");
                    }

                    Utility.ProvideBrowserResponse(context);

                    httpListener.Stop();

                    return code;
                }
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            finally
            {
            }
        }

        private static void RefreshAccessToken(string clientId, string secret, string refreshToken)
        {
            var postValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            };
            var tokenResponse = PerformHTTPPost(ConnectUrl, postValues);

            UnpackTokenResponse(tokenResponse);
        }

        private static void UnpackTokenResponse(string tokenResponse)
        { 
            var textReader = new StringReader(tokenResponse);
            var reader = new Newtonsoft.Json.JsonTextReader(textReader);
            while (reader.Read())
            {
                if (reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName && reader.Value is string)
                {
                    string key = reader.Path;

                    reader.Read();
                    if (reader.TokenType == Newtonsoft.Json.JsonToken.String)
                    {
                        string val = reader.Value as string;
                        Console.WriteLine($"tokenResponse  {key} = {val}");

                        if (key == "access_token")
                            access_token = val;

                        if (key == "refresh_token")
                            refresh_token = val;

                        if (key == "id_token")
                            id_token = val;
                    }
                    if (reader.TokenType == Newtonsoft.Json.JsonToken.Integer)
                    {
                        if (key == "expires_in")
                            access_token_expires = (long)reader.Value;
                    }
                }
            }

        }


        private static void RefreshVmsToken()
        {
            // Simply the same as first time
            ServerCommandServiceLogin(access_token, vmsLoginInstanceId);
            whenToRefreshVmsToken = DateTime.UtcNow + TimeSpan.FromMilliseconds(vms_token_expires_in / 1000);
        }

        /// <summary>
        /// Here we use the received code to retrieve an access token
        /// 
        /// The access token is part of a JSON response, that also contains a few other ids - but they are not used in this sample
        /// </summary>
        /// <param name="code"></param>
        /// <param name="codeVerifier"></param>
        /// <param name="redirectUri"></param>
        /// <param name="expectedNonce"></param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        private static void ExchangeCodeForToken(string code, string codeVerifier, string redirectUri, string expectedNonce, string clientId)
        {
            if (String.IsNullOrEmpty(code))
            {
                access_token = null;
                return;
            }

            string tokenResponse = ExchangeCodeForTokenResponse(code, codeVerifier, redirectUri, Scope, clientId);
            UnpackTokenResponse(tokenResponse);
            if (access_token == null)
                throw new Exception("Access token not found in token response");
        }

        /// <summary>
        /// Use the provided code to retrieve a TokenResponse in JSON format.
        /// </summary>
        /// <param name="code">Code from IDP used to exchange for tokens</param>
        /// <param name="codeVerifier">Used by IDP to ensure code is valid</param>
        /// <param name="redirectUri">Uri used to get the code</param>
        /// <param name="scope">The scope used in contacting IDP</param>
        /// <param name="clientId">Unique client id</param>
        /// <returns>Id token, access token and refresh token</returns>
        public static string ExchangeCodeForTokenResponse(string code, string codeVerifier, string redirectUri, string scope, string clientId)
        {
            var postValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("code_verifier", codeVerifier),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("scope", scope),
                new KeyValuePair<string, string>("client_id", clientId),
            };
            return PerformHTTPPost(ConnectUrl, postValues);
        }



        /// <summary>
        /// Get Well known configuration, and pick out the authorize and connect URLs
        /// TODO: Error handling and retry
        /// </summary>
        static void ProcessWellKnownConfiguration()
        {
            string uri = serverAddress + WellKnownConfigPath;
            string wellKnownConfiguration = PerformHTTPGet(uri);
            var textReader = new StringReader(wellKnownConfiguration);
            var reader = new Newtonsoft.Json.JsonTextReader(textReader);
            while (reader.Read())
            {
                if (reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName && reader.Value is string)
                {
                    string key = reader.Path;

                    reader.Read();
                    if (reader.TokenType == Newtonsoft.Json.JsonToken.String)
                    {
                        string val = reader.Value as string;
                        Console.WriteLine($".. {key} = {val}");

                        if (key == AuthorizationUrlKey)
                            AuthorizationUrl = val;
                        if (key == ConnectUrlKey)
                            ConnectUrl = val;
                        if (key == ExternalProvidersUrlKey)
                            ExternalProvidersUrl = val;
                    }
                }
            }
        }

        /// <summary>
        /// Get hold of external provider list, and pick out the first one
        /// </summary>
        static void ProcessExternalProviderList()
        {
            string uri = ExternalProvidersUrl;
            string wellKnownConfiguration = PerformHTTPGet(uri);

            var providers = Newtonsoft.Json.JsonConvert.DeserializeObject<ExternalProvider[]>(wellKnownConfiguration);
            if (providers.Length > 0)
                ExternalProviderId = providers[0].LoginProviderId;
            else
                ExternalProviderId = "OIDC";
        }

        /// <summary>
        /// Basic method to perform a HTTP request.
        /// Missing Error handling, certification check, async execution ...
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static string PerformHTTPGet(string url)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);

            webRequest.Method = "GET";
            webRequest.Timeout = 10000;

            using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using (StreamReader responseStream = new StreamReader(webResponse.GetResponseStream()))
                {
                    return responseStream.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Basic method to perform a HTTP POST request with url encoded parameters.
        /// Missing Error handling, certification check, async execution ...
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static string PerformHTTPPost(string url, List<KeyValuePair<string, string>> postValues)
        {
            StringBuilder bodyBuilder = new StringBuilder();
            bool first = true;
            foreach (var kv in postValues)
            {
                if (first)
                    first = false;
                else
                    bodyBuilder.Append("&");
                bodyBuilder.Append(kv.Key);
                bodyBuilder.Append("=");
                bodyBuilder.Append(Uri.EscapeDataString(kv.Value));
            }

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);

            webRequest.Method = "POST";
            webRequest.Timeout = 10000;
            var data = Encoding.ASCII.GetBytes(bodyBuilder.ToString());

            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.ContentLength = data.Length;
            using (var stream = webRequest.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using (StreamReader responseStream = new StreamReader(webResponse.GetResponseStream()))
                {
                    return responseStream.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Use the access_token for logging in on ServerCommandService and retrieve an vms_token
        /// </summary>
        /// <param name="access_token"></param>
        /// <returns>vms_token</returns>
        static void ServerCommandServiceLogin(string access_token, Guid instanceId)
        {
            var loginXml =
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<s:Body>" +
                "<Login xmlns=\"http://videoos.net/2/XProtectCSServerCommand\">" +
                "<instanceId>" + instanceId + "</instanceId>" +
                "<currentToken />" +
                "</Login>" +
                "</s:Body>" +
                "</s:Envelope>";

            var url = serverAddress + "/managementServer/ServerCommandServiceOauth.svc";
            var data = Encoding.ASCII.GetBytes(loginXml.ToString());

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);

            webRequest.Method = "POST";
            webRequest.Timeout = 10000;

            webRequest.ContentType = "text/xml; charset=utf-8";
            webRequest.Headers.Add("Authorization", "bearer " + access_token);
            webRequest.Headers.Add("SOAPAction", "http://videoos.net/2/XProtectCSServerCommand/IServerCommandService/Login");
            webRequest.ContentLength = data.Length;
            using (var stream = webRequest.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using (StreamReader responseStream = new StreamReader(webResponse.GetResponseStream()))
                {
                    string soapResponse = responseStream.ReadToEnd();
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(soapResponse);
                    XmlNode envelopeNode = GetNode(doc, "Envelope");
                    XmlNode bodyNode = GetNode(envelopeNode, "Body");
                    XmlNode loginResponseNode = GetNode(bodyNode, "LoginResponse");
                    XmlNode loginResultNode = GetNode(loginResponseNode, "LoginResult");
                    XmlNode timeToLiveNode = GetNode(loginResultNode, "TimeToLive");
                    XmlNode microSecondsNode = GetNode(timeToLiveNode, "MicroSeconds");
                    XmlNode tokenNode = GetNode(loginResultNode, "Token");

                    vms_token_expires_in = long.Parse(microSecondsNode.InnerText);
                    vms_token = tokenNode.InnerText;
                }
            }
        }

        private static XmlNode GetNode(XmlNode parent, string childName)
        {
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node.LocalName == childName)
                    return node;
            }
            return null;
        }
    }


    internal class ExternalProvider
    {
        public string LoginProviderId { get; set; }
        public string DisplayName { get; set; }
    }
}
