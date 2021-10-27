using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ServerCommandWrapper.OAuth
{
	/// <summary>
	/// Representation of Access Token returned by IDP server.
	/// </summary>
	public class AccessTokenResponse
	{
		/// <summary>
		/// The token
		/// </summary>
		public string Access_Token { get; set; }

		/// <summary>
		/// The expiration time in seconds
		/// </summary>
		public long Expires_In { get; set; }
	}

	/// <summary>
	/// Proxy to communicate with IDP server.
	/// </summary>
	public static class IdpClientProxy
	{
        /// <summary>
        /// Check if IDP server is available in VMS.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="prefix"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static async Task<bool> IsOAuthServer(string hostName, string prefix, int port)
		{
            Uri idpUri = GetIdpUri(hostName, prefix, port);
			var endpoint = new UriBuilder(idpUri)
			{
				Path = Path.Combine(idpUri.AbsolutePath, ".well-known/openid-configuration"),
			}.Uri.AbsoluteUri;
			var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
			try
			{
				var client = new HttpClient();
				var response = await client.SendAsync(request).ConfigureAwait(false);
				if (!response.IsSuccessStatusCode)
				{
					return false;
				}
				return HasOAuthServer(await response.Content.ReadAsStringAsync());
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Get a token for a user with password
		/// </summary>
		public static async Task<AccessTokenResponse> GetAccessTokenForBasicUserAsync(string hostName, string prefix, int port, string userName, string password)
		{
			HttpClient client = new HttpClient();
			client.BaseAddress = GetIdpUri(hostName, prefix, port);
			client.DefaultRequestHeaders.Accept.Clear();
			client.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));
			AccessTokenResponse accessTokenResponse = null;
			var postValues = new List<KeyValuePair<string, string>>()
			{
				new KeyValuePair<string, string>("grant_type", "password"),
				new KeyValuePair<string, string>("username", userName),
				new KeyValuePair<string, string>("password", password),
				new KeyValuePair<string, string>("client_id", "GrantValidatorClient"),
			};
			var request = new HttpRequestMessage(HttpMethod.Post, "connect/token")
			{
				Content = new FormUrlEncodedContent(postValues)
			};

			HttpResponseMessage response = await client.SendAsync(request);
			if (response.IsSuccessStatusCode)
			{
				accessTokenResponse = Read(await response.Content.ReadAsStringAsync());
			}
			return accessTokenResponse;
		}

        /// <summary>
        /// Get a token for the windows user specified in the constructor
        /// </summary>
        /// <param name="hostName">The base address of the identity server.</param>
        /// <param name="prefix"></param>
        /// <param name="credentials">The credentials used for authentication.</param>
        public static async Task<AccessTokenResponse> GetAccessTokenForWindowsUserAsync(string hostName, string prefix, int port, ICredentials credentials)
		{
			HttpClient client = new HttpClient(CreateClientHandler(credentials));
			client.BaseAddress = GetIdpUri(hostName, prefix, port);
			client.DefaultRequestHeaders.Accept.Clear();
			client.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));

			AccessTokenResponse accessTokenResponse = null;
			var postValues = new List<KeyValuePair<string, string>>()
			{
				new KeyValuePair<string, string>("grant_type", "windows_credentials"),
				new KeyValuePair<string, string>("client_id", "GrantValidatorClient"),
			};
			HttpContent content = new FormUrlEncodedContent(postValues);
			var request = new HttpRequestMessage(HttpMethod.Post, "connect/token") { Content = content };

			HttpResponseMessage response = await client.SendAsync(request);
			if (response.IsSuccessStatusCode)
			{
				accessTokenResponse = Read(await response.Content.ReadAsStringAsync());
			}
			return accessTokenResponse;
		}

		private static HttpClientHandler CreateClientHandler(ICredentials credentials = null)
		{
			return new HttpClientHandler()
			{
				Credentials = credentials,
				UseDefaultCredentials = credentials == null,
			};
		}

		private static Uri GetIdpUri(string hostName, string prefix, int port)
		{
			var uri = new UriBuilder(prefix == "https" ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, hostName, port).Uri;
            return new Uri(uri, @"/idp\");
		}

		/// <summary>
		/// In the sample we are using regular expressions for simplicity,
		/// but as the data are in json format you can easily use a standard json library for parsing the data.
		/// </summary>
		/// <param name="httpContent"></param>
		/// <returns></returns>
		private static AccessTokenResponse Read(string httpContent)
		{
			string patternAccessToken = "\"access_token\":\".*?\"";
			string patternExpiryTime = "\"expires_in\":[0-9]{1,}";
			Regex rgxAccessToken = new Regex(patternAccessToken);
			Regex rgxExpiryTime = new Regex(patternExpiryTime);
			var result1 = rgxAccessToken.Match(httpContent);
			var result2 = rgxExpiryTime.Match(httpContent);
			if (result1.Success && result2.Success)
			{
				return new AccessTokenResponse()
						{
							Access_Token = result1.Value.Replace("\"", "").Split(':')[1],
							Expires_In = Convert.ToInt32(result2.Value.Replace("\"", "").Split(':')[1])
						};
			}
			else
			{
				return null;
			}
		}
		
		/// <summary>
		/// In the sample we are using regular expressions for simplicity,
		/// but as the data are in json format you can easily use a standard json library for parsing the data.
		/// </summary>
		/// <param name="httpContent"></param>
		/// <returns></returns>
		private static bool HasOAuthServer(string httpContent)
		{
			string pattern = "\"server_version\"" + ":" + "\"[0-9]{1,2}\\.[0-9]";
			Regex rgx = new Regex(pattern);
			var result = rgx.Match(httpContent);
			if (result.Success)
			{
				string[] serverVersion = result.Value.Replace("\"", "").Split(':')[1].Split('.');
				int serverMajorVersion = Convert.ToInt32(serverVersion[0]);
				return serverMajorVersion >= 21;
			}
			else
			{
				return false;
			}
		}
	}
}
