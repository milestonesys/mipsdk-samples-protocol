#define PLAY_WITH_DOTNET

using ServerCommandWrapper;
using ServerCommandWrapper.Basic;
using ServerCommandWrapper.Ntlm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.ServiceModel;
using System.Threading;
using System.Windows;
// VideoOS.Platform.Proxy.Alarm is defined in auto-generated AlarmCommand.cs
// It contains the definitions for getting multiple AlarmLines at a time. An AlarmLine is the "short version" of an Alarm
using VideoOS.Platform.Proxy.Alarm;

// milestonesystems is also defined in auto-generated AlarmCommand.cs and AlarmCommandToken.cs
// It contains the definitions for getting and setting one Alarm at a time with all details available.
// ----------------------------------------------------------------------------------------------------------------------------------
//
// This program shows how to query alarms from the Milestone XProtect Event Server's database
// It allows you to set a search filter, in this case NAME="My own alarm" SORTBY=TIME, DESCENDING
// It makes a query every second and if the newest alarm is newer than any alarm seen before, a wav file is played
// The sample also shows statistics about the total number of alarms per state in the database
// To use the sample, you need to fill out the credentials settings according to your environment and set the "CurrentSetup" variable
// Environment: Windows, C#, .NET 4.0. No Milestone libraries required, WDSL generated client included as AlarmCommand.cs and AlarmCommandToken.cs
//
// ----------------------------------------------------------------------------------------------------------------------------------


namespace AlarmList
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const Setup CurrentSetup = Setup.DefaultWindowsUser;
        private static readonly Guid IntegrationId = new Guid("483324DE-B1F0-4702-8487-EA4A0EB4BF53");
        private const string IntegrationName ="Alarm List";
        private const string Version = "1.0";
        private enum Setup { BasicUser, WindowsUser, DefaultWindowsUser };
        private const string DomainForBasicUser = "BASIC";
        private bool _isBasicUser = false;
        private int _management_server_ssl_port = 443;
        private string _userName = null;
        private string _password = null;
        private string _domainName = null;
        private BasicConnection _basicLogin = null;
        private NtlmConnection _ntlmLogin = null;
        private LoginInfo _loginInfo;
        private string _token = null;
        private long _timeToLive = 0;
        private Timer _tokenExpireTimer;
        private int MESSAGE_BUFFER_SIZE = 2000000;        
        private Thread _thread;
        private AlarmCommandClient _alarmCommandClient;
        private AlarmCommandTokenClient _alarmCommandTokenClient;
        AlarmFilter _filter = new AlarmFilter();
        OrderBy _orderBy = new OrderBy();
        DateTime _lastUsedTimestamp = DateTime.Now.ToUniversalTime();
        Random _rand = new Random();
        private bool _exit = false;
        private delegate void OnAlarmsReceived();
        private delegate void OnStatsReceived();
        private OnAlarmsReceived _onAlarmsReceived;
        private OnStatsReceived _onStatsReceived;
        private AlarmLine[] _alarms = null;
        private Statistic[] _stats = null;
        private ObservableCollection<ObservableAlarmLine> _alarmObserverCollection = new ObservableCollection<ObservableAlarmLine>();
        private string _address;
        private string _hostName;
        private int _port = 80;
        private string _alarmName;
        private int _statsNew = 0;
        private int _statsInProgress = 0;
        private int _statsOnHold = 0;
        private int _statsClosed = 0;
        private string _musicfile = string.Empty;
#if PLAY_WITH_DOTNET
        private System.Media.SoundPlayer _player = new System.Media.SoundPlayer();        
#endif

        public MainWindow()
        {
            InitializeComponent();
            _buttonStop.IsEnabled = false;

            // Prepare to show the data received
            _onAlarmsReceived = new OnAlarmsReceived(onAlarmsReceivedMethod);
            _onStatsReceived = new OnStatsReceived(onStatsReceivedMethod);
            _listViewAlarms.ItemsSource = _alarmObserverCollection;
            this.DataContext = this;
        }

        /// <summary>
        /// Configure which alarms you wish to request
        /// </summary>
        private void ConfigureFilter()
        {
            // Create a new list of conditions
            List<VideoOS.Platform.Proxy.Alarm.Condition> cList = new List<VideoOS.Platform.Proxy.Alarm.Condition>();

            // Create a new condition. 
            VideoOS.Platform.Proxy.Alarm.Condition cond = new VideoOS.Platform.Proxy.Alarm.Condition();

            // In this case we want to select all alarms with a specific message entered in the upper entry field
            cond.Operator = Operator.Contains;
            cond.Target = Target.Message;
            cond.Value = _alarmName;
            // Add the condition to the list of conditions
            cList.Add(cond);

            // Filtering on priority 1 will in almost any case give some alarms
            // cond.Operator = Operator.Equals;
            // cond.Target = Target.Priority;
            // cond.Value = 1;
            // Add the condition to the list of conditions
            // cList.Add(cond);

            // You could create more conditions and add them. They would be ANDed.

            // Apply the list of conditions to our alarm filter
            _filter.Conditions = cList.ToArray();

            // We want the newest alarms first
            _orderBy.Order = Order.Descending;
            _orderBy.Target =Target.Timestamp;
            _filter.Orders = new OrderBy[] { _orderBy };
        }

        /// <summary>
        /// This is the entry point for the worker thread requesting alarms in the background
        /// </summary>
        private void AlarmThread()
        {
            SetAuthentication();
            Login();
            if (OpenClient())
            {
                while (!_exit)
                {
                    if (!GetAlarmData())
                        break;
                    Thread.Sleep(3000);
                }
            }
        }

        private void SetAuthentication()
        {
            switch (CurrentSetup)
            {
                case Setup.BasicUser:
                    _userName = "";
                    _password = "";
                    _domainName = DomainForBasicUser;
                    _isBasicUser = true;
                    break;
                case Setup.WindowsUser:
                    _userName = "";
                    _password = "";
                    _domainName = "";
                    break;
                case Setup.DefaultWindowsUser:
                    _userName = CredentialCache.DefaultNetworkCredentials.UserName;
                    _password = CredentialCache.DefaultNetworkCredentials.Password;
                    _domainName = CredentialCache.DefaultNetworkCredentials.Domain;
                    break;
            }
        }
        private void Login()
        {
            Uri uri = new UriBuilder(_address).Uri;
            _hostName = uri.Host;
            _port = uri.Port;
            if (_isBasicUser)
            {
                _basicLogin = new BasicConnection(_userName, _password, _hostName, _management_server_ssl_port);
                try
                {
                    _loginInfo = _basicLogin.Login(IntegrationId, Version, IntegrationName);
                    _token = _loginInfo.Token;
                    _timeToLive = (long) _loginInfo.TimeToLive.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    MessageBox.Show("Unable to login: " + e.Message);
                    return;
                }

            }
            else
            {
                var authType = CurrentSetup == Setup.WindowsUser
                    ? AuthenticationType.Windows
                    : AuthenticationType.WindowsDefault;
                _ntlmLogin = new NtlmConnection(_domainName, authType, _userName, _password, _hostName, _port);

                try
                {
                    _loginInfo = _ntlmLogin.Login(IntegrationId, Version, IntegrationName); 
                    _token = _loginInfo.Token;
                    _timeToLive = (long) _loginInfo.TimeToLive.TotalMilliseconds;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to login: " + ex.Message);
                    return;
                }

            }
            long timeout = (int)_timeToLive;
            _tokenExpireTimer = new Timer(_tokenExpiredCallBack, null, timeout, Timeout.Infinite);
        }

        private void _tokenExpiredCallBack(object state)
        {
            Login();
        }

        /// <summary>
        /// Creates and opens an instance of the WSDL generated client from AlarmCommandToken.cs or AlarmCommand.cs
        /// </summary>
        /// <returns></returns>
        private bool OpenClient()
        {
            try
            {
                int port = 22331;
                if (_isBasicUser)
                {
                    BasicHttpBinding binding = new BasicHttpBinding();
                    binding.MaxReceivedMessageSize = MESSAGE_BUFFER_SIZE;
                    binding.MaxBufferSize = MESSAGE_BUFFER_SIZE;
                    binding.MaxBufferPoolSize = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxArrayLength = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxBytesPerRead = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxNameTableCharCount = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxDepth = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxStringContentLength = MESSAGE_BUFFER_SIZE;

                    Uri uri = new UriBuilder(Uri.UriSchemeHttp, _hostName, port, "Central/AlarmServiceToken").Uri;
                    string alarmServiceUri = uri.AbsoluteUri;
                    var endpointAddress = new EndpointAddress(alarmServiceUri);

                    _alarmCommandTokenClient = new AlarmCommandTokenClient(binding, endpointAddress);

                    try
                    {
                        _alarmCommandTokenClient.Open();
                        if (!(_alarmCommandTokenClient.State != CommunicationState.Opening ||
                              _alarmCommandTokenClient.State != CommunicationState.Opened))
                        {
                            ResetUiDelegate resetUiDelegate = new ResetUiDelegate(resetUi);
                            _buttonStop.Dispatcher.Invoke(resetUiDelegate);
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Error opening AlarmCommandTokenClient: " + e.Message + ((e.InnerException != null) ? (" - " + e.InnerException.Message) : ""));
                        ResetUiDelegate resetUiDelegate = new ResetUiDelegate(resetUi);
                        _buttonStop.Dispatcher.Invoke(resetUiDelegate);
                        return false;
                    }
                }
                else
                {
                    WSHttpBinding binding = new WSHttpBinding(SecurityMode.None);
                    binding.MaxReceivedMessageSize = MESSAGE_BUFFER_SIZE;
                    binding.MaxBufferPoolSize = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxArrayLength = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxBytesPerRead = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxNameTableCharCount = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxDepth = MESSAGE_BUFFER_SIZE;
                    binding.ReaderQuotas.MaxStringContentLength = MESSAGE_BUFFER_SIZE;
                    binding.Security.Mode = SecurityMode.Message;
                    binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
                    binding.Security.Transport.ProxyCredentialType = HttpProxyCredentialType.None;
                    binding.Security.Message.ClientCredentialType = MessageCredentialType.Windows;
                    binding.Security.Message.EstablishSecurityContext = false;
                    binding.Security.Message.NegotiateServiceCredential = true;
                    var uri = new UriBuilder(Uri.UriSchemeHttp, _hostName, port, "/Central/AlarmService2").Uri;
                    var endpointAddress = new EndpointAddress(uri, EndpointIdentity.CreateSpnIdentity(BasicConnection.SpnFactory.GetSpn(uri)));

                    _alarmCommandClient = new AlarmCommandClient(binding, endpointAddress);
                    _alarmCommandClient.ClientCredentials.Windows.ClientCredential.UserName = _userName;
                    _alarmCommandClient.ClientCredentials.Windows.ClientCredential.Password = _password;
                    try
                    {
                        _alarmCommandClient.Open();
                        if (!(_alarmCommandClient.State != CommunicationState.Opening ||
                              _alarmCommandClient.State != CommunicationState.Opened))
                        {
                            ResetUiDelegate resetUiDelegate = new ResetUiDelegate(resetUi);
                            _buttonStop.Dispatcher.Invoke(resetUiDelegate);
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Error opening AlarmCommandClient: " + e.Message + ((e.InnerException != null) ? (" - " + e.InnerException.Message) : ""));
                        ResetUiDelegate resetUiDelegate = new ResetUiDelegate(resetUi);
                        _buttonStop.Dispatcher.Invoke(resetUiDelegate);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error opening Client: " + e.Message + ((e.InnerException != null) ? (" - " + e.InnerException.Message) : ""), "Exception");
                ResetUiDelegate resetUiDelegate = new ResetUiDelegate(resetUi);
                _buttonStop.Dispatcher.Invoke(resetUiDelegate);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Query a list of filtered alarm lines and the statistics for all alarms on this server
        /// </summary>
        /// <returns></returns>
        private bool GetAlarmData()
        {
            // We need an array of alarm lines to receive data to. An alarm line is the "short version" of an alarm
            try
            {
                // We want to have a rolling window always showing the 10 newest alarms matching the filter
                // That is ok for a sample program playing Xmas songs. Your application probably needs to be somewhat smarter.                
                // We also want to get some general statistics

                if (_isBasicUser)
                {
                    _alarms = _alarmCommandTokenClient.GetAlarmLines(_token,  0, 10, _filter);
                    _stats = _alarmCommandTokenClient.GetStatistics(_token);
                } else
                {
                    _alarms = _alarmCommandClient.GetAlarmLines(0, 10, _filter);
                    _stats = _alarmCommandClient.GetStatistics();
                }

                // We need to update the UI in the UI thread. Please note that if you want to access the list view from other threads also, you must provide the thread safety.
                if (_alarms != null)
                    Dispatcher.Invoke(_onAlarmsReceived);
                // We need to update the UI in the UI thread. Please note that if you want to access the list view from other threads also, you must provide the thread safety.
                if (_stats != null)
                    Dispatcher.Invoke(_onStatsReceived);
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "Exception");
                ResetUiDelegate resetUiDelegate = new ResetUiDelegate(resetUi);
                _buttonStop.Dispatcher.Invoke(resetUiDelegate);
                return false;
            }

            // If one of the alarms is all new (newer than latest new alarm), sound the alarm
            if (_alarms != null && _alarms.Length > 0)
            {
                if (_alarms[0].Timestamp > _lastUsedTimestamp)
                {
                    _lastUsedTimestamp = _alarms[0].Timestamp;
                    SoundTheAlarm();
                }
            }
            return true;
        }

        /// <summary>
        /// Play a random .wav file from MyMusic\AlarmSounds
        /// </summary>
        private void SoundTheAlarm()
        {
            try            
            {
                // Play a random wav file from subdirectory Xmas in MyMusic, rescan directory every time to allow new music to be dropped in
                string mymusicpath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                string myXmasmusicpath = System.IO.Path.Combine(mymusicpath, "AlarmSounds");
                System.IO.DirectoryInfo dirInfo = new System.IO.DirectoryInfo(myXmasmusicpath);
                System.IO.FileInfo[] files = dirInfo.GetFiles("*.wav");
                _musicfile = files[_rand.Next(0, files.Length - 1)].Name;

#if PLAY_WITH_DOTNET
                _player.Stop();
                _player.SoundLocation = System.IO.Path.Combine(myXmasmusicpath, _musicfile);
                _player.Play();
#else
                // This allows you to use the installed player, which hopefully plays more file formats than the .NET player
                System.Diagnostics.Process.Start(System.IO.Path.Combine(myXmasmusicpath, _musicfile));
#endif
            }
            catch (Exception)
            {
                // If the directory MyMusic\AlarmSounds was not created, or if no wav files were put there, we silently continue
            }
        }

        public delegate void ResetUiDelegate();

        /// <summary>
        /// Show the statistics data about all alarms
        /// </summary>
        public void onStatsReceivedMethod()
        {
            foreach (Statistic stat in _stats)
            {
                if (stat.Type == StatisticType.State)
                {
                    switch ((AlarmStates)stat.Number)
                    {
                        case AlarmStates.New:
                            _statsNew = stat.Value;
                            NotifyPropertyChanged("StatsNew");
                            break;
                        case AlarmStates.InProgress:
                            _statsInProgress = stat.Value;
                            NotifyPropertyChanged("StatsInProgress");
                            break;
                        case AlarmStates.OnHold:
                            _statsOnHold = stat.Value;
                            NotifyPropertyChanged("StatsOnHold");
                            break;
                        case AlarmStates.Closed:
                            _statsClosed = stat.Value;
                            NotifyPropertyChanged("StatsClosed");
                            break;
                        default:
                            break;
                    }
                    NotifyPropertyChanged("StatsTotal");
                }
            }
        }

        public string StatsNew { get { return string.Format("New: {0}", _statsNew); } }
        public string StatsInProgress { get { return string.Format("In progress: {0}", _statsInProgress); } }
        public string StatsOnHold { get { return string.Format("On hold: {0}", _statsOnHold); } }
        public string StatsClosed { get { return string.Format("Closed: {0}", _statsClosed);} }
        public string StatsTotal { get { return string.Format("Total: {0}", _statsNew + _statsInProgress + _statsOnHold + _statsClosed); } }

        /// <summary>
        /// Display a smoothly scrolling list of the 10 newest alarms
        /// </summary>
        public void onAlarmsReceivedMethod()
        {
            try
            {
                // If you know that your alarms are all new, you may clear the observable list and fill in from scratch, but that may cause flickering.
                // The algorithm here is to avoid such flickering. It is a mini version of the code from the Smart Client plug-in.

                int bottomIndex = _alarms.Length - 1;
                for (int i = 0; i <= bottomIndex ; i++) 
                {
                    // We assume that the alarms received are always ordered by time descending, and the Alarm.ID is unique per instance of an Alarm
                    // Thus it is given that if a given ID is ordered after another ID once, it will always be after that other ID.
                    //
                    // There are two possibilities
                    // A: The received item is new and does not appear in the collection,
                    //    in which case we just add it to the top of the collection, but after those new ones already added
                    // B: The received item appears at this or a later position in the old collection
                    //    in which case the old ones from the current position to the match position must have disappeared
                    //    All those items are removed and the matching item is updated

                    ObservableAlarmLine alarm = new ObservableAlarmLine(_alarms[i]);
                    int match = FindId(i, alarm.AlarmLine.Id);
                    if (match == -1)
                    {
                        // The alarm is inserted as a new item, because it did not appear in the former collection
                        _alarmObserverCollection.Insert(i, alarm);
                    }
                    else
                    {
                        // If the match was some positions ahead, we are sure that those alarms skipped shall no longer be displayed
                        // Remember not to advance the index passed to RemoveAt(). When we have deleted position i, the old i+1 is not at the same position.
                        for (int j = i; j < match; j++)
                        {
                            _alarmObserverCollection.RemoveAt(i);
                        }

                        // We must then update the individual properties of the alarm in the list with those property values from the new alarm
                        // Just setting _alarmObserverCollection[i] = alarm cause the WPF list view's multiple selection mechanism to get confused.
                        UpdateCollectionItem(i, alarm);
                    }
                }

                // If the number of alarms received is less than in the original collection, we are left with a tail of alarms, which we don't know the actual status of
                // Most likely, they will exist unaltered, but they may have had their data changed or they might have been deleted. We do not know that for sure.
                // This implementation therefore removes them, which may cause selected items to disappear from the list view
                // The exception is an empty list caused by a temporary connect error, where we want to keep all the old data
                // If the list really it to be cleared, _eraseAlarmListBeforeUpdate should have been set to true
                if (bottomIndex > 0)
                {
                    int n = _alarmObserverCollection.Count - 1;
                    while (n > bottomIndex)
                    {
                        _alarmObserverCollection.RemoveAt(n);
                        n--;
                    }
                }
            }
            catch (Exception)
            {
                // Go on. This is just for safety.
            }
        }

        /// <summary>
        /// Find a given alarm in the old list
        /// </summary>
        /// <param name="i">index in old list to start from</param>
        /// <param name="id">GUID of individual alarm to find</param>
        /// <returns></returns>
        private int FindId(int i, Guid id)
        {
            for (int j = i; j < _alarmObserverCollection.Count; j++)
            {
                if (_alarmObserverCollection[j].AlarmLine.Id == id)
                    return j;
            }
            return -1;
        }

        /// <summary>
        /// Update an entry in the list with data from an alarm
        /// </summary>
        /// <param name="i">Index in the list to update</param>
        /// <param name="alarm">Alarm with the new data to display</param>
        private void UpdateCollectionItem(int i, ObservableAlarmLine alarm)
        {
            // Local ID and message are assumed not to change
            if (_alarmObserverCollection[i].Priority != alarm.Priority)
            {
                _alarmObserverCollection[i].Priority = alarm.Priority;
            }
            if (_alarmObserverCollection[i].State != alarm.State)
            {
                _alarmObserverCollection[i].State = alarm.State;
            }
        }

        private void resetUi()
        {
            _textBoxServer.IsEnabled = true;
            _textBoxAlarmName.IsEnabled = true;
            _buttonGo.IsEnabled = true;
            _buttonStop.IsEnabled = false;
            try
            {
                _listViewAlarms.Items.Clear();
            }
            catch
            {
                // Ignore
            }
        }

        /// <summary>
        /// Exit the application
        /// </summary>
        /// <param name="sender">Not used</param>
        /// <param name="e">Not used</param>
        private void Button_Click_Stop(object sender, RoutedEventArgs e)
        {
            if (_thread != null)
            {
                _thread.Abort();
                _thread = null;
            }
            resetUi();
        }

        /// <summary>
        /// Start the application receiving alarms continuously in a worker thread
        /// </summary>
        /// <param name="sender">Not used</param>
        /// <param name="e">Not used</param>
        private void Button_Click_Go(object sender, RoutedEventArgs e)
        {
            if (_thread != null)
            {
                // This ought not happen, but just in case...
                _thread.Abort();
                _thread = null;
            }

            _address = _textBoxServer.Text;
            _alarmName = _textBoxAlarmName.Text;
            ConfigureFilter();
            // Have a worker thread request alarms in the background
            _thread = new Thread(AlarmThread);
            _thread.Start();
            _textBoxServer.IsEnabled = false;
            _textBoxAlarmName.IsEnabled = false;
            _buttonGo.IsEnabled = false;
            _buttonStop.IsEnabled = true;
        }

        /// <summary>
        /// Before exiting, remember to tell the worker thread to terminate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _exit = true;
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
        #endregion
    }
}
