using ServerCommandWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml;


namespace TcpVideoViewer
{
    /// <summary>
    /// Interaction logic for WindowMain.xaml
    /// </summary>
    public partial class WindowMain : Window , INotifyPropertyChanged
    {
        private SystemAccess _sysInfo = new SystemAccess();
        private List<SystemAccess.Camera> _cameras = new List<SystemAccess.Camera>();
        private Thread _recvThread = null;
        private ImageServerConnection _isc = null;
        private bool _connected = false;
        private List<long> _sequenceStartTimes = null;
        private List<long> _sequenceEndTimes = null;

        public WindowMain()
        {
            InitializeComponent();
            _mainGrid.DataContext = this;
            _serverName.Text = _sysInfo.Server;
            _sysInfo.OnTokenRefreshed += Token_Refreshed;
            NotifyPropertyChanged("TokenString");
            _tokenTextBlock.Visibility = Visibility.Hidden;
            _tokenTextBlock.Visibility = Visibility.Visible;
        }

        private void Token_Refreshed(object sender, string e)
        {
            TokenString = e;
            _sysInfo.LoginInfo.Token = TokenString;
            NotifyPropertyChanged("TokenString");
            Dispatcher.Invoke(() =>
            {
                _tokenTextBlock.Visibility = Visibility.Hidden;
                _tokenTextBlock.Visibility = Visibility.Visible;
            });           

            if (_isc != null)
            {
                _isc.SetToken(TokenString);

                // We need to update the Token on the recording server:
                // This will pass the token back on the live imageserver TCP session using the CONNECTUPDATE command
                // DoLiveCmd() will do nothing if there is no live session
                _isc.DoLiveCmd(_isc.FormatConnectUpdate());

                // This will cause an active playback to pass the token back on the imageserver TCP session using the CONNECTUPDATE command
                // If no playback session is active, this will cause no change.
                _isc.PlaybackSendConnectUpdateFlag = true;
            }
        }

        private void About_Button_Click(object sender, RoutedEventArgs e)
        {
            string str =
                "This is a Milestone MIP sample showing how to access live and recorded video data using the Image Server TCP protocol.\r\n\r\n" +
                "It is written in C# with Visual Studio using the .NET 4.0 HttpWebRequest, NetworkStream and SslStream if need be. No library from Milestone is applied.\r\n\r\n" +
                "In principle, this code can with some effort be ported to any programming environment which supports HTTP with NTLM authentication and Socket features.\r\n\r\n" + 
                "If using SSL the trusted server Certificate must be added on this client machine, and porting might not be as simple.\r\n\r\n";
            MessageBox.Show(str, "MIP Sample: TCP Video Viewer");
        }

        private void Connect_Button_Click(object sender, RoutedEventArgs e)
        {
            _sysInfo.Connect(_serverName.Text);
            LoginInfo loginInfo = _sysInfo.LoginInfo;
            TokenString = loginInfo.Token;

            NotifyPropertyChanged("TokenString");
            _tokenTextBlock.Visibility = Visibility.Hidden;
            _tokenTextBlock.Visibility = Visibility.Visible;

            _cameras = _sysInfo.GetSystemCameras();
            
            foreach (SystemAccess.Camera camera in _cameras)
            {
                _cameraCombo.Items.Add(camera.Name);
            }
            _cameraCombo.SelectedIndex = 0;

            _connected = true;
            NotifyPropertyChanged("LiveButtonVisibility");
            NotifyPropertyChanged("CameraComboVisibility");
            NotifyPropertyChanged("ConnectButtonVisibility");
            NotifyPropertyChanged("ServerTextBoxEnabled");
            NotifyPropertyChanged("CameraComboEnabled");
        }

        private void Live_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_recvThread != null)
                return;

            int i = _cameraCombo.SelectedIndex;
            SystemAccess.Camera camera = _cameras[i];
            _isc = new ImageServerConnection(camera.RecorderUri, camera.Guid.ToString(), 75);
            _isc.SetCredentials(_sysInfo);
            _isc.OnImageReceivedMethod += OnLiveImageReceivedMethod;
            _isc.OnConnectionStoppedMethod += OnLiveConnectionStoppedMethod;
            _isc.OnStatusItemReceivedMethod += OnStatusItemReceivedMethod;
            _isc.RenderObject = this;
            _recvThread = new Thread(_isc.Live);
            _recvThread.Start();

            PlaybackTimeString = "Live";
            NotifyPropertyChanged("PlaybackTimeString");
            NotifyPropertyChanged("LiveButtonVisibility");
            NotifyPropertyChanged("StopButtonVisibility");
            NotifyPropertyChanged("CameraComboEnabled");
            NotifyPropertyChanged("TokenString");
            _tokenTextBlock.Visibility = Visibility.Hidden;
            _tokenTextBlock.Visibility = Visibility.Visible;
        }

        private void _sequenceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_recvThread != null || _sequenceList.SelectedItem == null || _sequenceList.SelectedItem.Equals("no"))
                return;

            DateTime date = new DateTime();
            string str = _sequenceList.SelectedItem is string ? (string)_sequenceList.SelectedItem : string.Empty;
            if (!DateTime.TryParse(str.Substring(0,8), out date))
                return;

            int i = _cameraCombo.SelectedIndex;
            SystemAccess.Camera camera = _cameras[i];
            _isc = new ImageServerConnection(camera.RecorderUri, camera.Guid.ToString(), 75);
            _isc.SetCredentials(_sysInfo);
            _isc.OnImageReceivedMethod +=OnPlaybackImageReceivedMethod;
            _isc.OnConnectionStoppedMethod += OnPlaybackConnectionStoppedMethod;
            _isc.RenderObject = this;
            _isc.PlaybackStartTime = _sequenceStartTimes[_sequenceList.SelectedIndex];
            _isc.PlaybackEndTime = _sequenceEndTimes[_sequenceList.SelectedIndex]; ;
            _recvThread = new Thread(_isc.Playback);
            _recvThread.Start();

            NotifyPropertyChanged("LiveButtonVisibility");
            NotifyPropertyChanged("StopButtonVisibility");
            NotifyPropertyChanged("CameraComboEnabled");
            NotifyPropertyChanged("TokenString");
            _tokenTextBlock.Visibility = Visibility.Hidden;
            _tokenTextBlock.Visibility = Visibility.Visible;
        }

        private void _cameraCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_recvThread != null)
                return;

            VideoImage = null;
            NotifyPropertyChanged("VideoImage");
            PlaybackTimeString = "";
            NotifyPropertyChanged("PlaybackTimeString");            

            int i = _cameraCombo.SelectedIndex;
            SystemAccess.Camera camera = _cameras[i];
            ImageServerConnection isc = new ImageServerConnection(camera.RecorderUri, camera.Guid.ToString(), 75);
            isc.SetCredentials(_sysInfo);
            string sequences = isc.GetSequences(DateTime.Now, 16);

            _sequenceList.Items.Clear();
            _sequenceStartTimes = new List<long>();
            _sequenceEndTimes = new List<long>();
            if (sequences.StartsWith("#"))
            {
                _sequenceList.Items.Add(sequences.Substring(1));
                return;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(sequences);
                XmlNodeList nodes = doc.GetElementsByTagName("alarm");
                foreach (XmlNode node in nodes)
                {
                    XmlAttributeCollection attrs = node.Attributes;
                    string start = "";
                    string end = "";
                    string alarm = "??";

                    for (int ii = 0; ii < attrs.Count; ii++)
                    {
                        XmlAttribute attr = (XmlAttribute)attrs.Item(ii);
                        if (attr.Name.ToLower().Equals("starttime"))
                            start = attr.Value;
                        if (attr.Name.ToLower().Equals("endtime"))
                            end = attr.Value;
                        if (attr.Name.ToLower().Equals("alarmtime"))
                            alarm = attr.Value;

                    }

                    long ss = long.Parse(start);
                    long ee = long.Parse(end);
                    DateTime sdt = TimeConverter.From(start);
                    DateTime edt = TimeConverter.From(end);
                    TimeSpan sql = edt - sdt;

                    _sequenceList.Items.Insert(0, sdt.ToLongTimeString() + " (" + sql.Seconds + " s)");
                    _sequenceStartTimes.Insert(0, ss);
                    _sequenceEndTimes.Insert(0, ee);
                }
            }
            catch (Exception ex)
            {
                string s = ex.Message;
            }
        }

        private void Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_recvThread == null)
                return;

            _isc.StopLive();
            _isc.StopPlayback();

            NotifyPropertyChanged("TokenString");
            _tokenTextBlock.Visibility = Visibility.Hidden;
            _tokenTextBlock.Visibility = Visibility.Visible;
        }

        public void OnStatusItemReceivedMethod(object p)
        {
        }

        public void OnLiveConnectionStoppedMethod(object p)
        {
            _isc = null;
            _recvThread = null;
            PlaybackTimeString = String.Format("Stopped at {0}", DateTime.Now.ToString("HH:mm:ss"));
            NotifyPropertyChanged("PlaybackTimeString");
            NotifyPropertyChanged("StopButtonVisibility");
            NotifyPropertyChanged("LiveButtonVisibility");
            NotifyPropertyChanged("CameraComboEnabled");
        }

        public void OnPlaybackConnectionStoppedMethod(object p)
        {
            _isc = null;
            _recvThread = null;
            NotifyPropertyChanged("StopButtonVisibility");
            NotifyPropertyChanged("LiveButtonVisibility");
            NotifyPropertyChanged("CameraComboEnabled");
        }

        public void OnLiveImageReceivedMethod(object p)
        {
            ImageInfo ii = (ImageInfo)p;
            VideoImage = (byte[])ii.Data;
            NotifyPropertyChanged("VideoImage");
        }

        public void OnPlaybackImageReceivedMethod(object p)
        {
            ImageInfo ii = (ImageInfo)p;
            VideoImage = (byte[])ii.Data;
            NotifyPropertyChanged("VideoImage");
            PlaybackTimeString = TimeConverter.From(ii.Current).ToString("HH:mm:ss.fff");
            NotifyPropertyChanged("PlaybackTimeString");
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_recvThread != null)
                _recvThread.Abort();
        }
        

        #region Events and Protected Methods

        /// <summary>
        /// Needed for coordination with WPF controls
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Needed for coordination with WPF controls
        /// </summary>
        /// <param name="propertyName"></param>
        protected void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public byte[] VideoImage { get; private set; } = null;

        public string PlaybackTimeString { get; private set; } = string.Empty;

        public string TokenString { get; private set; } = "Not logged in. No token.";

        public Visibility StopButtonVisibility
        {
            get
            {
                if (_recvThread == null)
                    return Visibility.Collapsed;
                return Visibility.Visible;
            }
        }

        public Visibility LiveButtonVisibility
        {
            get
            {
                if (_recvThread == null && _connected)
                    return Visibility.Visible;
                return Visibility.Collapsed;
            }
        }

        public bool CameraComboEnabled
        {
            get
            {
                if (_connected && _recvThread == null)
                    return true;
                return false;
            }
        }

        public Visibility CameraComboVisibility
        {
            get
            {
                if (_connected)
                    return Visibility.Visible;
                return Visibility.Collapsed;
            }
        }

        public Visibility ConnectButtonVisibility
        {
            get
            {
                if (_connected)
                    return Visibility.Collapsed;
                return Visibility.Visible;
            }
        }

        public bool ServerTextBoxEnabled
        {
            get
            {
                if (_connected)
                    return false;
                return true;
            }
        }

        #endregion
    }
}
