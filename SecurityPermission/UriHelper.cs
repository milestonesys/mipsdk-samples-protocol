using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SecurityPermission
{
    /// <summary>
    /// Assist with getting the correct URI / URLs for logon to IDP and use of the API Gateway
    /// </summary>
    public class UriHelper
    {
        private const string WellKnownUris = "api/.well-known/uris";
        private const string ServerUpTestUri = ".well-known/uris";      // Use something that does not need authorization
        private const string ApiGatewayRestPrefix = "rest/v1";

        private const string WellKnownIDPConfigPath = "/.well-known/openid-configuration";
        private const string ConnectUrlKey = "token_endpoint";

        private static int HttpTimeoutInSeconds = 5;                 // Used for HTTP Requests 


        private Uri _serverAddressUri;


        /// These are the primary used URIs for XProtect
        /// This is the root address of the API Gateway
        public Uri ApiGatewayUri { get; private set; }
        /// <summary>
        /// This is the root of the Rest API
        /// </summary>
        public Uri ApiGatewayRestUri { get; private set; }
        /// <summary>
        /// This should be used for authorization and getting tokens
        /// </summary>
        public Uri IdpConnectUri { get; private set; }
        /// <summary>
        /// The is the root uri for the IDP service
        /// </summary>
        public Uri IDPUri { get; private set; }

        public UriHelper(Uri serverAddressUri)
        {
            _serverAddressUri = serverAddressUri;
        }

        public bool Initialize()
        {
            GetWellKnownApiGatewayInfo(_serverAddressUri);
            if (IDPUri == null)
                return false;
            ProcessIDPWellKnownConfiguration(IDPUri);
            return true; 
        }

        /// <summary>
        /// Loop through installed ApiGateways, and pick out one that works.
        /// When complete, the ApiGatewayUri contains the ApiGateway Uri or null if not found
        /// </summary>
        /// <param name="serverUri"></param>
        private void GetWellKnownApiGatewayInfo(Uri serverUri)
        {
            Uri requestUri = new Uri(serverUri, WellKnownUris);
            string json = PerformHTTPGet(requestUri);
            if (json != null)
            {
                ApiGatewayWellKnownInfo wellKnownInfo = JsonConvert.DeserializeObject<ApiGatewayWellKnownInfo>(json);
                if (wellKnownInfo != null)
                {
                    if (wellKnownInfo.ApiGateways != null)
                    {
                        if (wellKnownInfo.ApiGateways.Length == 1)
                            ApiGatewayUri = new Uri(wellKnownInfo.ApiGateways[0]);
                        else
                            ApiGatewayUri = PickOneApiGateway(wellKnownInfo.ApiGateways);
                    }
                    ApiGatewayRestUri = new Uri(ApiGatewayUri, ApiGatewayRestPrefix);
                    IDPUri = new Uri(wellKnownInfo.IdentityProvider);
                }
                return;
            }
            IDPUri = null;
            ApiGatewayUri = null;
        }

        private Uri PickOneApiGateway(string[] allApiGateways)
        {
            if (allApiGateways.Length > 0)
            {
                // Start at any index in the array, and loop 
                int index = RandomIndex(allApiGateways.Length);
                int startIndex = index;
                do
                {
                    string uriString = allApiGateways[index];
                    Uri apiServerUri = new Uri(uriString);
                    if (PerformHTTPGet(new Uri(apiServerUri, ServerUpTestUri)) != null) // See if API gw has contact to VMS
                    {
                        return apiServerUri;
                    }
                    index++;
                    if (index >= allApiGateways.Length)
                        index = 0;
                } while (index != startIndex);
            }
            return null;
        }

        private static int RandomIndex(int max)
        {
            Random random = new Random(DateTime.Now.Millisecond);
            return random.Next(max);
        }

        /// <summary>
        /// Get Well known configuration, and pick out the authorize and connect URLs
        /// </summary>
        private void ProcessIDPWellKnownConfiguration(Uri identityProviderUri)
        {
            Uri requestUri = new Uri(identityProviderUri + WellKnownIDPConfigPath);
            string json = PerformHTTPGet(requestUri);
            if (json != null)
            {
                dynamic wellKnownInfo = JsonConvert.DeserializeObject<dynamic>(json);
                if (wellKnownInfo.token_endpoint != null)
                {
                    IdpConnectUri = new Uri(wellKnownInfo.token_endpoint.Value);
                    return;
                }
            }
            IdpConnectUri = null;
        }


        /// <summary>
        /// Perform a simple unauthorized HTTP Get request
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static string PerformHTTPGet(Uri url)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(HttpTimeoutInSeconds);

                try
                {
                    HttpResponseMessage response = client.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (response.Content == null)
                        return null;

                    string responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        return null;

                    return responseContent;
                }
                catch (HttpRequestException)        // Like: Server offline
                {
                    return null;
                }
                catch (TaskCanceledException)
                {
                    return null;
                }
            }

        }

    }

    public class ApiGatewayWellKnownInfo
    {
        public string[] ApiGateways;
        public string ProductVersion;
        public string IdentityProvider;
    }
}
