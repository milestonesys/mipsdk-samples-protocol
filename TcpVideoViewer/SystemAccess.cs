using ServerCommandWrapper;
using ServerCommandWrapper.Basic;
using ServerCommandWrapper.Ntlm;
using System;
using System.Collections.Generic;


namespace TcpVideoViewer
{
    /// <summary>
    /// Proxy class to hold the user credentials and manage the logic between the server and the client
    /// </summary>
    public class SystemAccess
    {
        private static readonly Guid IntegrationId = new Guid("BE07504F-B330-4475-9AE4-1A7FF10BD486");
        private const string IntegrationName = "TCP Video Viewer";
        private const string Version = "1.0";
        public AuthenticationType AuthenticationType = AuthenticationType.WindowsDefault;

        public String Server = "localhost";
        public String User = "";
        public String Password = "";
        public String Domain = "";

        public LoginInfo LoginInfo;

        private NtlmConnection _ntlmConnection;
        private BasicConnection _basicConnection;

        public event EventHandler<string> OnTokenRefreshed = delegate { };

        /// <summary>
        /// Connect to the specified server
        /// </summary>
        /// <param name="server">URL of the server</param>
        public void Connect(String server)
        {
            if (_basicConnection != null)
            {
                _basicConnection.OnTokenRefreshed -= _connection_OnTokenRefreshed;
                _basicConnection.Logout();
                _basicConnection = null;
            }
            if (_ntlmConnection != null)
            {
                _ntlmConnection.OnTokenRefreshed -= _connection_OnTokenRefreshed;
                _ntlmConnection.Logout();
                _ntlmConnection = null;
            }
            Server = server;
            switch (AuthenticationType)
            {
                case AuthenticationType.Basic:
                    {
                        int port = 443;
                        _basicConnection = new BasicConnection(User, Password, Server, port);
                        LoginInfo = _basicConnection.Login(IntegrationId, Version, IntegrationName);
                        _basicConnection.OnTokenRefreshed += _connection_OnTokenRefreshed;
                        break;
                    }
                case AuthenticationType.Windows:
                case AuthenticationType.WindowsDefault:
                    {
                        _ntlmConnection = new NtlmConnection(Domain, AuthenticationType, User, Password, Server);
                        LoginInfo = _ntlmConnection.Login(IntegrationId, Version, IntegrationName);
                        _ntlmConnection.OnTokenRefreshed += _connection_OnTokenRefreshed;
                        break;
                    }
                default:
                    //empty
                    break;
            }
        }

        private void _connection_OnTokenRefreshed(object sender, string e)
        {
            OnTokenRefreshed.Invoke(this, e);
        }

        /// <summary>
        /// Get the cameras available on the server
        /// </summary>
        /// <returns></returns>
        public List<Camera> GetSystemCameras()
        {
            if (LoginInfo?.Token == null)
                return new List<Camera>();

            switch (AuthenticationType)
            {
                case AuthenticationType.Basic:
                    _basicConnection.GetConfiguration(LoginInfo.Token);
                    return ExtractCameraDataFrom(_basicConnection.ConfigurationInfo);

                case AuthenticationType.Windows:
                case AuthenticationType.WindowsDefault:
                    _ntlmConnection.GetConfiguration(LoginInfo.Token);
                    return ExtractCameraDataFrom(_ntlmConnection.ConfigurationInfo);

                default:
                    return new List<Camera>();
            }
        }

        /// <summary>
        /// Extract the camera data from a <see cref="ServerCommandService.ConfigurationInfo"/> object
        /// </summary>
        /// <param name="confInfo">The configuration that contains the information about the cameras</param>
        /// <returns>The extracted list of cameras</returns>
        public List<Camera> ExtractCameraDataFrom(ServerCommandService.ConfigurationInfo confInfo)
        {
            List<Camera> cameras = new List<Camera>();
            foreach (ServerCommandService.RecorderInfo recorder in confInfo.Recorders)
            {
                foreach (ServerCommandService.CameraInfo cameraInfo in recorder.Cameras)
                {
                    Camera cam = new Camera();

                    int colonIndex = recorder.WebServerUri.LastIndexOf(':');
                    int slashIndex = recorder.WebServerUri.LastIndexOf('/');
                    String portStr = recorder.WebServerUri.Substring(colonIndex + 1, slashIndex - colonIndex - 1);

                    cam.Guid = cameraInfo.DeviceId;
                    cam.Name = cameraInfo.Name;
                    cam.RecorderUri = new Uri(recorder.WebServerUri);

                    cameras.Add(cam);
                }
            }

            return cameras;
        }

        /// <summary>
        /// Simple representation of a camera
        /// </summary>
        public struct Camera
        {
            /// <summary>
            /// The GUID of the camera
            /// </summary>
            public Guid Guid;

            /// <summary>
            /// Name of the camera
            /// </summary>
            public String Name;

            /// <summary>
            /// Uri of where the camera is located
            /// </summary>
            public Uri RecorderUri;
        }
    }
}
