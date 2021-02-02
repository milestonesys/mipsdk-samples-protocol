using System;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Text;
using System.Threading;
using System.Xml;
using ServerCommandService;


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

        private readonly String _serverUrl;
        private readonly int _port;
        private readonly AuthenticationType _authType;
        private readonly String _username;
        private readonly String _password;
        private readonly String _domain;

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
         public NtlmConnection(String domain, AuthenticationType authType, String username, String password,
            String hostname, int port = 80)
        {
            //Precondition
            if (hostname.StartsWith("http://"))
                hostname = hostname.Substring("http://".Length);

            _serverUrl = hostname;
            _port = port;
            _authType = authType;
            _username = username;
            _password = password;
            _domain = domain;

            String url;
            String prefix = "http";

            if (_port == 443) 
                prefix += "s";

            url = $"{prefix}://{hostname}:{_port}/ManagementServer/ServerCommandService.svc";
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
            Server.ClientCredentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication()
            {
                CertificateValidationMode = X509CertificateValidationMode.None,
            };           

        }



        /// <summary>
        /// Login to the server
        /// </summary>        
        private LoginInfo Login() 
        {
            string currentToken = "";
            

            if (_loginInfo != null)
                currentToken = _loginInfo.Token;
            // Now call the login method on the server, and get the loginInfo class (provide old token for next re-login)
            _loginInfo = Server.Login(_thisInstance, currentToken);

            // React 30 seconds before token expires. (Never faster than 30 seconds after last renewal, but that ought not occur).
            // Default timeout is 1 hour.
            double ms = LoginInfo.TimeToLive.TotalMilliseconds;
            ms = ms > 60000 ? ms - 30000 : ms;

            _tokenExpireTimer = new Timer(TokenExpireTimer_Callback, null, (int)ms, Timeout.Infinite);

            return LoginInfo;
        }

        /// <summary>
        /// Login to the server, and register the integration if applicable
        /// </summary>        
        public LoginInfo Login(Guid integrationId, string version, string integrationName)
        {
            string currentToken = "";


            if (_loginInfo != null)
                currentToken = _loginInfo.Token;
            // Now call the login method on the server, and get the loginInfo class (provide old token for next re-login)
            _loginInfo = Server.Login(_thisInstance, currentToken);

            try
            {
                Server.RegisterIntegration(_loginInfo.Token, Constants.InstanceId, integrationId, version, integrationName, Constants.ManufacturerName);
            }
            catch
            {
                // on older systems this method does not exist so we will just silently ignore any exceptions - and with no retry. Consequences of failing are ignorable anyways
            }

            // React 30 seconds before token expires. (Never faster than 30 seconds after last renewal, but that ought not occur).
            // Default timeout is 1 hour.
            double ms = LoginInfo.TimeToLive.TotalMilliseconds;
            ms = ms > 60000 ? ms - 30000 : ms;

            _tokenExpireTimer = new Timer(TokenExpireTimer_Callback, null, (int)ms, Timeout.Infinite);

            return LoginInfo;
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
