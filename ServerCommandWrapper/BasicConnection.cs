using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Text;
using System.Threading;
using System.Xml;
using ServerCommandService;

namespace ServerCommandWrapper.Basic
{
    /// <summary>
    /// Class to control a connection for basic authentications.
    /// </summary>
    public class BasicConnection
    {
        public ServerCommandServiceClient Server { get; }

        private ServerCommandService.LoginInfo _loginInfo;

        private readonly Uri _uri;
        private readonly Guid _thisInstance = Guid.NewGuid();
        private Timer _tokenExpireTimer;

        private readonly String _serverUrl;
        private readonly int _port;
        private readonly String _username;
        private readonly String _password;

        /// <summary>
        /// Subscribe to this to be notified when token changes
        /// </summary>
        public event EventHandler<string> OnTokenRefreshed = delegate { };

        /// <summary>
        /// Configuration if the server is a C server.
        /// Please collect the configuration first, using <see cref="GetConfiguration"/>
        /// </summary>
        public ServerCommandService.ConfigurationInfo ConfigurationInfo;

     
        /// <summary>
        /// Constructor for the BasicConnection, which performs and sets some start-up routines
        /// </summary>
        /// <param name="username">Username to use</param>
        /// <param name="password">Password to use</param>
        /// <param name="hostname">The hostname</param>
        /// <param name="port">Which port to use</param>
        public BasicConnection(String username, String password, String hostname, int port)
        {
            //Precondition
            if (hostname.StartsWith("http://"))
                hostname = hostname.Substring("http://".Length);

            _serverUrl = hostname;            
            _username = username;
            _password = password;
            _port = port;

            // SSL
            _uri = new Uri($"https://{_serverUrl}:{_port}/ManagementServer/ServerCommandService.svc");

            // Create Soap class from interface
            Server = new ServerCommandServiceClient(GetBinding(),
                new EndpointAddress(_uri, EndpointIdentity.CreateSpnIdentity(SpnFactory.GetSpn(_uri))));

            // Set basic credentials
            Server.ClientCredentials.UserName.UserName = username;
            Server.ClientCredentials.UserName.Password = password;
            // TODO Any certificate is accepted as OK !!
            Server.ClientCredentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication()
            {
                CertificateValidationMode = X509CertificateValidationMode.None,
            };

            // Open service (internally)
            Server.Open();

            // Give OS a few milliseconds to get ready
            long tickStop = DateTime.Now.Ticks + 1000000; // 1.000.000 ticks == 100 ms
            while (DateTime.Now.Ticks < tickStop && Server.State == CommunicationState.Opening)
            {
                Thread.Sleep(5);
            }
        
        }

        /// <summary>
        /// The current login information
        /// </summary>
        public LoginInfo LoginInfo
        {
            get
            {
                return LoginInfo.CreateFrom(_loginInfo);
            }
        }

        /// <summary>
        /// Login to the server       
        /// </summary>      
        /// <returns>Info of valid log in</returns>
        private LoginInfo Login() 
        {
            string currentToken = "";
            if (LoginInfo != null)
                currentToken = LoginInfo.Token;

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
        /// <returns>Info of valid log in</returns>
        public LoginInfo Login(Guid integrationId, string version, string integrationName)
        {
            string currentToken = "";
            if (LoginInfo != null)
                currentToken = LoginInfo.Token;

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
            Server.Logout(_thisInstance, LoginInfo.Token);
            _loginInfo = null;
            CancelCallbackTimer();
        }

        /// <summary>
        /// Gets the version of the server
        /// </summary>
        /// <returns>The version as an int</returns>
        public int GetVersion()
        {
            return Server.GetVersion();
        }

        /// <summary>
        /// Gets the configuration from the server.
        /// Automatically determines if the SOAP interface or the XML file should be used
        /// </summary>
        /// <param name="token">Valid token (only used with C servers)</param>
        public void GetConfiguration(String token = "")
        {
            ConfigurationInfo = Server.GetConfiguration(token);            
        }

        /// <summary>
        /// Callback method to perform a login and thereby refresh the token.
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
        /// Cancels the callback timer and thereby stops refreshing the token before it expires
        /// </summary>
        private void CancelCallbackTimer()
        {
            _tokenExpireTimer.Dispose();
            _tokenExpireTimer = null;
        }

        /// <summary>
        /// Gets the binding to use for a Basic authentication model
        /// </summary>
        /// <returns>The binding to use</returns>
        private static System.ServiceModel.Channels.Binding GetBinding()
        {
            BasicHttpBinding binding = new BasicHttpBinding();
            binding.Security.Mode = BasicHttpSecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            binding.MaxBufferPoolSize = Int32.MaxValue;
            binding.MaxReceivedMessageSize = Int32.MaxValue;
            //binding.ReaderQuotas = XmlDictionaryReaderQuotas.Max;     // can be set when GetCameraInfoFromConfiguration is needed (and is big)
            return binding;
        }

        /// <summary>
        /// The SpnFactory is a helper class to get the right SPN for a connection
        /// </summary>
        public static class SpnFactory
        {
            private const string SpnTemplate = "VideoOS/{0}:{1}";
            private static string _localHostFqdn = null;

            /// <summary>
            /// GetSpn returns the right SPN for a connection on the specified URI
            /// </summary>
            /// <param name="serverUri">The URI of the service to be connected</param>
            /// <returns>A valid SPN for the service</returns>
            public static string GetSpn(Uri serverUri)
            {
                if (serverUri == null)
                {
                    throw new ArgumentNullException("serverUri");
                }

                string host = serverUri.Host;
                if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    if (String.IsNullOrEmpty(_localHostFqdn))
                    {
                        _localHostFqdn = Dns.GetHostEntry("localhost").HostName;
                    }

                    host = _localHostFqdn;
                }

                var spn = String.Format(CultureInfo.InvariantCulture, SpnTemplate, host, serverUri.Port);
                return spn;
            }
        }
    }
}