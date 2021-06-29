using ServerCommandService;
using ServerCommandWrapper.OAuth;
using System;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Threading;
using System.Threading.Tasks;
using static ServerCommandWrapper.Basic.BasicConnection;

namespace ServerCommandWrapper.Ntlm
{
	/// <summary>
	/// A class to represent the NTLM connection between the client a server.
	/// </summary>
	public class NtlmConnection
    {
        public ServerCommandServiceClient Server
        {
            get;
        } 
        
        private readonly Guid _thisInstance = Guid.NewGuid();
        private Timer _tokenExpireTimer;

        private readonly String _hostName;
        private readonly bool _isOAuthServer;
        private readonly int _port;
        private readonly AuthenticationType _authType;
        private readonly String _username;
        private readonly String _password;
        private readonly String _domain;

        // Set to http if your IDP server is running not secure
        // Only relevant for servers in version 2021R1 or newer
        private const string OAuthServerPrefix = "https";

        private ServerCommandService.LoginInfo _loginInfo;

        /// <summary>
        /// Subscribe to this to be notified when token changes
        /// </summary>
        public event EventHandler<string> OnTokenRefreshed = delegate { };

        /// <summary>
        /// The configuration information if the server is a C server.
        /// Please perform a call to <see cref="GetConfiguration"/> before using it.
        /// </summary>
        public ServerCommandService.ConfigurationInfo ConfigurationInfo;

        /// <summary>
        /// Information about the login
        /// </summary>
        public LoginInfo LoginInfo
        {
            get
            {
                return LoginInfo.CreateFrom(_loginInfo);
            }
        }


        /// <summary>
        /// Constructor of the NtlmConnection class. Will perform and set the needed start-up routines
        /// </summary>
        /// <param name="domain">The domain (may be empty)</param>
        /// <param name="authType">Authentication type</param>
        /// <param name="username">The username</param>
        /// <param name="password">The password related to the username</param>
        /// <param name="hostname">The hostname</param>
        /// <param name="port">The used port</param>
        public NtlmConnection(String domain, AuthenticationType authType, String username, String password, String hostname, int port = 80)
        {
            //Precondition
            string prefix = "http";
            if (hostname.StartsWith("http://"))
            {
                hostname = hostname.Substring("http://".Length);
            }

            if (hostname.StartsWith("https://"))
            {
                hostname = hostname.Substring("https://".Length);
                prefix = "https";
            }

            _hostName = hostname;
            _authType = authType;
            _username = username;
            _password = password;
            _domain = domain;

            _isOAuthServer = Task.Run(() => IdpClientProxy.IsOAuthServer(_hostName, OAuthServerPrefix, port == 80 ? 443 : port)).GetAwaiter().GetResult();
            if (_isOAuthServer)
            {
                _port = port == 80 ? 443 : port;
	            // If the OAuth server is available it uses OAuth version of ServerCommandService.
				var uri = ManagementServerOAuthHelper.CalculateServiceUrl(hostname, _port, OAuthServerPrefix);
	            var oauthBinding = ManagementServerOAuthHelper.GetOAuthBinding(isHttps: OAuthServerPrefix == "https");
	            string spn = SpnFactory.GetSpn(uri);
	            EndpointAddress endpointAddress = new EndpointAddress(uri, EndpointIdentity.CreateSpnIdentity(spn));
	            Server = new ServerCommandServiceClient(oauthBinding, endpointAddress);
            }
            else
            {
                _port = port;
                if (_port == 443 && prefix != "https")
                    prefix = "https";

                var url = $"{prefix}://{hostname}:{_port}/ManagementServer/ServerCommandService.svc";
	            WSHttpBinding binding = new WSHttpBinding()
	            {
		            MaxReceivedMessageSize = 1000000
	            };
	            EndpointAddress remoteAddress = new EndpointAddress(url);

	            Server = new ServerCommandServiceClient(binding, remoteAddress);
	            Server.ClientCredentials.Windows.ClientCredential.UserName = username;
	            Server.ClientCredentials.Windows.ClientCredential.Password = password;
	            if (!String.IsNullOrEmpty(_domain))
		            Server.ClientCredentials.Windows.ClientCredential.Domain = _domain;

	            // TODO Any certificate is accepted as OK !!
	            Server.ClientCredentials.ServiceCertificate.SslCertificateAuthentication =
		            new X509ServiceCertificateAuthentication()
		            {
			            CertificateValidationMode = X509CertificateValidationMode.None,
		            };
            }
        }


        /// <summary>
        /// Login to the server
        /// </summary>        
        private LoginInfo Login() 
        {
	        string currentToken = "";
	        double accessTokenTimeToLive = 0;


	        if (_loginInfo != null)
		        currentToken = _loginInfo.Token;

	        if (_isOAuthServer)
	        {
		        NetworkCredential nc = _authType == AuthenticationType.WindowsDefault
			        ? null
			        : new NetworkCredential(_username, _password, _domain);
		        var accessToken = Task
			        .Run(() => IdpClientProxy.GetAccessTokenForWindowsUserAsync(_hostName, OAuthServerPrefix, _port, nc)).GetAwaiter()
			        .GetResult();
		        if (accessToken != null) // Access token is received from Identity server in exchange user credentials.
		        {
			        // Check if the behavior already exists, otherwise will throw an exception when re-login.
                    if (Server.Endpoint.EndpointBehaviors.Contains(typeof(AddTokenBehavior)))
			        {
				        Server.Endpoint.EndpointBehaviors.Remove(typeof(AddTokenBehavior));
			        }
			        // Access token is added to the request header through the EndpointBehaviour, thus user credentials are no longer
			        // needed to get the corporate token. 
                    Server.Endpoint.EndpointBehaviors.Add(new AddTokenBehavior(accessToken.Access_Token));
			        ManagementServerOAuthHelper.ConfigureEndpoint(Server.Endpoint);
                    // Default expiry time for the access token is 3600 seconds.
			        accessTokenTimeToLive = TimeSpan.FromSeconds(accessToken.Expires_In).TotalMilliseconds;
		        }
	        }

	        // Now call the login method on the server, and get the loginInfo class (provide old token for next re-login)
	        _loginInfo = Server.Login(_thisInstance, currentToken);

            // If access token is available then take the minimum time to live between access token and corporate token (token) as expiry time.
	        // React 30 seconds before token expires. (Never faster than 30 seconds after last renewal, but that ought not occur).
	        // Default timeout is 1 hour.
	        double ms = accessTokenTimeToLive != 0 ? Math.Min(LoginInfo.TimeToLive.TotalMilliseconds, accessTokenTimeToLive) : LoginInfo.TimeToLive.TotalMilliseconds;
	        ms = ms > 60000 ? ms - 30000 : ms;

	        _tokenExpireTimer = new Timer(TokenExpireTimer_Callback, null, (int)ms, Timeout.Infinite);

	        return LoginInfo;
        }

        /// <summary>
        /// Login to the server, and register the integration if applicable
        /// </summary>        
        public LoginInfo Login(Guid integrationId, string version, string integrationName)
        {
	        var loginInfo = Login();
            try
            {
                Server.RegisterIntegration(_isOAuthServer ? "" : _loginInfo.Token, Constants.InstanceId, integrationId, version, integrationName, Constants.ManufacturerName);
            }
            catch
            {
                // on older systems this method does not exist so we will just silently ignore any exceptions - and with no retry. Consequences of failing are ignorable anyways
            }

            return loginInfo;
        }

        /// <summary>
        /// Logout from the server
        /// </summary>
        public void Logout()
        {
            Server.Logout(_thisInstance, _loginInfo.Token);
            _loginInfo = null;
            CancelCallbackTimer();
        }

        /// <summary>
        /// Get version of the host server
        /// </summary>
        /// <returns>Version of the server</returns>
        public int GetVersion()
        {
            return Server.GetVersion();
        }

        /// <summary>
        /// Get the configuration and store it in the property of this class
        /// </summary>        
        /// <param name="token">The valid token, received when logged in</param>        
        public void GetConfiguration(String token)
        {
            ConfigurationInfo = Server.GetConfiguration(token);
        }
       
        /// <summary>
        /// Callback method which will perform a new login to refresh the token
        /// </summary>
        /// <param name="state">Not used</param>
        private void TokenExpireTimer_Callback(Object state)
        {
            try
            {
                var loginInfo = Login();
                
                if (String.IsNullOrEmpty(loginInfo.Token))
                    throw new Exception("Got blank token when trying to refresh");

                OnTokenRefreshed.Invoke(this, loginInfo.Token);
            }
            catch (Exception e)
            {
                CancelCallbackTimer();
                throw new Exception("Error refreshing token: " + e.Message);
            }
        }

        /// <summary>
        /// Stop the <see cref="TokenExpireTimer_Callback"/> from being called anymore, which refreshes the token in time
        /// </summary>
        private void CancelCallbackTimer()
        {
            _tokenExpireTimer.Dispose();
            _tokenExpireTimer = null;            
        }
    }
}
