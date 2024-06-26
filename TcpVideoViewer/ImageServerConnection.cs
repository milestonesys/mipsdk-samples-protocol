using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Xml;

using System.Windows.Controls; // Limitation: This code is hardcoded to WPF style callbacks.
using System.Linq;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;

namespace TcpVideoViewer
{
    class ImageServerConnection
    {
        public delegate void OnImageReceived(object p);

        public delegate void OnConnectionStopped(object p);

        public delegate void OnPresetsReceived(object p);

        public delegate void OnStatusItemReceived(object p);

        private object _renderObject;
        private bool _live = false;
        private bool _playback = false;
        private long _playbackTime;
        private Stream _streamLive = null;
        private int _reqCounter = 0;
        private string _token = "";
        private string _user = "";
        private string _pwd = "";
        private string _cameraGuid = "";
        private Uri _imageServer = null;
        private int _quality = 100;
        private int _speed = 1;
        private object _liveSocketSendLock = new object();
        private bool _playbackSendConnectUpdateFlag = false;

        public byte[] LastJPEG { get; private set; } = null;

#if DEBUG
        public ImageServerConnection() // For testing on static camera
        {
            _imageServer = new UriBuilder(Uri.UriSchemeHttp, "192.168.235.131", 7563).Uri;
            _cameraGuid = "a9adc052-e793-4ed0-a1e2-8b975ba8e020";
            _token = "";
        }
#endif

        public ImageServerConnection(Uri imageServer, string cameraGuid, int qual)
        {
            _imageServer = imageServer;
            _cameraGuid = cameraGuid;
            _quality = qual;
        }

        public void SetBasicCredentials(string user, string pwd)
        {
            _token = "BASIC";
            _user = user;
            _pwd = pwd;
        }

        public void SetTokenCredentials(string token)
        {
            _token = token;
            _user = "#";
            _pwd = "#";
        }

        public void SetToken(string token)
        {
            _token = token;
        }

        public void SetCredentials(SystemAccess sysInfo)
        {
            if (sysInfo.LoginInfo.Token != "BASIC")
                SetTokenCredentials(sysInfo.LoginInfo.Token);
            else
                SetBasicCredentials(sysInfo.User, sysInfo.Password);
        }

        public OnStatusItemReceived OnStatusItemReceivedMethod { set; get; } = null;

        public OnImageReceived OnImageReceivedMethod { set; get; } = null;

        public OnConnectionStopped OnConnectionStoppedMethod { set; get; } = null;

        public OnPresetsReceived OnPresetsReceivedMethod { set; get; } = null;

        public object RenderObject
        {
            set { _renderObject = value; }
        }

        public bool PlaybackSendConnectUpdateFlag
        {
            set { _playbackSendConnectUpdateFlag = value; }
        }

        public long PlaybackStartTime { set; get; }

        public long PlaybackEndTime { set; get; }

        public void StopLive()
        {
            _live = false;
            _streamLive = null;
        }

        public void StopPlayback()
        {
            _playback = false;
        }

        public string FormatConnectUpdate()
        {
            string sendBuffer = string.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>{0}</requestid>" +
                "<methodname>connectupdate</methodname>" +
                "<connectparam>id={1}&amp;connectiontoken={2}</connectparam>" +
                "</methodcall>\r\n\r\n",
                ++_reqCounter, _cameraGuid, _token);

            return sendBuffer;
        }

        private string FormatLive()
        {
            string sendBuffer;

            if (_quality == 100 || _quality < 1 || _quality > 104)
            {
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>1</requestid>" +
                    "<methodname>live</methodname>" +
                    "<compressionrate>90</compressionrate>" +
                    "</methodcall>\r\n\r\n");
            }
            else
            {
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>1</requestid>" +
                    "<methodname>live</methodname>" +
                    "<compressionrate>{0}</compressionrate>" +
                    "</methodcall>\r\n\r\n",
                    _quality);
            }

            return sendBuffer;
        }



        private string XmlEscapeGt127(string raw)
        {
            string str = "";

            foreach (char c in raw)
            {
                if (c < 128)
                {
                    str += c;
                }
                else
                {
                    str += string.Format("&#{0};", Convert.ToUInt32(c));
                }
            }

            return str;
        }

        private string FormatConnect()
        {
            string sendBuffer;

            if (_token == "BASIC")
            {
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>0</requestid>" +
                    "<methodname>connect</methodname><username>{0}</username><password>{1}</password>" +
                    "<cameraid>{2}</cameraid><alwaysstdjpeg>yes</alwaysstdjpeg>" +
                    "</methodcall>\r\n\r\n",
                    XmlEscapeGt127(_user), XmlEscapeGt127(_pwd), _cameraGuid);
            }
            else
            {
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>0</requestid>" +
                    "<methodname>connect</methodname><username>a</username><password>a</password>" +
                    "<cameraid>{0}</cameraid><alwaysstdjpeg>yes</alwaysstdjpeg>" +
                    "<transcode><allframes>yes</allframes></transcode>" + // Add this line to get all frames in a GOP transcoded
                    "<connectparam>id={1}&amp;connectiontoken={2}" +
                    "</connectparam></methodcall>\r\n\r\n",
                    _cameraGuid, _cameraGuid, _token);
            }

            return sendBuffer;
        }

        private Stream ConnectToImageServer()
        {
            Stream networkStream = null;
            String oper = "";
            try
            {
                IPAddress ipAddress;
                oper = "new Socket";
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    oper = "IPAddress.Parse " + _imageServer;
                    ipAddress = IPAddress.Parse(_imageServer.Host);
                }
                catch
                {
                    oper = "ConnInfo.ToIpv4 " + _imageServer;
                    ipAddress = ConnInfo.ToIpv4(_imageServer.Host);
                }

                oper = "new IPEndPoint " + ipAddress;
                IPEndPoint ipe = new IPEndPoint(ipAddress, _imageServer.Port);
                sock.Connect(ipe);

                oper = "NetworkStream";
                networkStream = new NetworkStream(sock, true);

                if (String.Equals(_imageServer.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    oper = "SslStream";
                    SslStream sslStream = new SslStream(networkStream, false);
                    networkStream = sslStream;
                    sslStream.AuthenticateAsClient(_imageServer.Host);
                }

                networkStream.ReadTimeout = 10000;
                networkStream.WriteTimeout = 2000;
            }
            catch (AuthenticationException ae)
            {
                // Tell the application I'm done
                if (OnConnectionStoppedMethod != null)
                {
                    Control pj = (Control)_renderObject;
                    string errMsg = $"SSL error {ae.Message}.";
                    pj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                        new ConnInfo(IscErrorCode.NoConnect, errMsg));
                }
                networkStream?.Dispose();
                networkStream = null;
            }
            catch (IOException ioe)
            {
                // Tell the application I'm done
                if (OnConnectionStoppedMethod != null)
                {
                    Control pj = (Control)_renderObject;
                    string errMsg = $"NetworkStream error {ioe.Message}.";
                    pj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                        new ConnInfo(IscErrorCode.NoConnect, errMsg));
                }
                networkStream?.Dispose();
                networkStream = null;
            }
            catch (SocketException se)
            {
                // Tell the application I'm done
                if (OnConnectionStoppedMethod != null)
                {
                    Control pj = (Control)_renderObject;
                    string errMsg = $"Socket error {se.ErrorCode}. Win32 error {se.NativeErrorCode}";
                    pj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                        new ConnInfo(IscErrorCode.NoConnect, errMsg));
                }
                networkStream?.Dispose();
                networkStream = null;
            }
            catch (Exception)
            {
                // Tell the application I'm done
                if (OnConnectionStoppedMethod != null)
                {
                    Control pj = (Control)_renderObject;
                    pj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                        new ConnInfo(IscErrorCode.NoConnect, oper));
                }
                networkStream?.Dispose();
                networkStream = null;
            }

            return networkStream;
        }


        public void Playback()
        {
            Stream networkStream = null;

            try
            {

                networkStream = ConnectToImageServer();
                // Errors are handled by ConnectToImageServer
                if (networkStream == null) return;

                _playback = true;
                int maxbuf = 1024 * 64;

                string sendBuffer = FormatConnect();

                // Deliberately not encoded as UTF-8
                // With XPCO and XPE/WinAuth only the camera GUID and the token are used. These are always 7 bit ASCII
                // With XPE/BasicAuth, the username and password are in clear text. The server expect bytes in it's own current code page.
                // Encoding this as UTF-8 will prevent corrent authentication with other than 7-bit ASCII characters in username and password
                // Encoding this with "Default" will at least make other than 7-bit ASCII work when client's and server's code pages are alike
                // XPE's Image Server Manager has an option of manually selecting a code page.
                // But there is no way in which a client can obtain the XPE server's code page selection.
                Byte[] bytesSent = Encoding.Default.GetBytes(sendBuffer);
                networkStream.Write(bytesSent, 0, bytesSent.Length);

                Byte[] bytesReceived = new Byte[maxbuf];

                int bytes = RecvUntil(networkStream, bytesReceived, 0, maxbuf);
                string page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                PlaybackSendConnectUpdateFlag = false; // We just got a new token, old renewal requests can be ignored.

                bool authenticated = false;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(page);
                XmlNodeList nodes = doc.GetElementsByTagName("connected");
                foreach (XmlNode node in nodes)
                {
                    if (node.InnerText.ToLower() == "yes")
                    {
                        authenticated = true;
                    }
                }

                if (!authenticated)
                {
                    // Tell the application I'm done
                    if (OnConnectionStoppedMethod != null)
                    {
                        Control pj = (Control) _renderObject;
                        pj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                            new Object[] {new ConnInfo(IscErrorCode.InvalidCredentials, "")});
                    }

                    return;
                }

                int count = 1;
                bool atEnd = false;
                _playbackTime = PlaybackStartTime;
                while (_playback && !atEnd)
                {
                    if (_playbackSendConnectUpdateFlag)
                    {
                        _playbackSendConnectUpdateFlag = false;
                        sendBuffer = FormatConnectUpdate();
                        bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                        networkStream.Write(bytesSent, 0, bytesSent.Length);

                        bytes = RecvUntil(networkStream, bytesReceived, 0, maxbuf);
                    }

                    int curBufSize = maxbuf;

                    int qual = _quality;
                    if (_quality < 1 || _quality > 104)
                    {
                        qual = 100;
                    }

                    sendBuffer = string.Format(
                        "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>{0}</requestid>" +
                        "<methodname>goto</methodname>" +
                        "<time>{1}</time>" +
                        "<compressionrate>{2}</compressionrate>" +
                        "<keyframesonly>no</keyframesonly>" +
                        "</methodcall>\r\n\r\n",
                        ++count, _playbackTime, qual);

                    bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                    networkStream.Write(bytesSent, 0, bytesSent.Length);

                    bytes = RecvUntil(networkStream, bytesReceived, 0, maxbuf);
                    if (bytes < 0)
                    {
                        throw new Exception("Receive error A");
                    }

                    page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                    if (bytesReceived[0] == '<')
                    {
                        // This is XML status message
                        continue;
                    }

                    if (bytesReceived[0] == 'I')
                    {
                        // Image
                        ImageInfo h = ParseHeader(bytesReceived, 0, bytes);
                        DateTime cur = TimeConverter.From(h.Current);

                        // Taste two first bytes
                        bytes = RecvFixed(networkStream, bytesReceived, 0, 2);
                        bytes = Math.Abs(bytes);
                        if (bytes != 2)
                        {
                            throw new Exception("Receive error 2");
                        }

                        // if (h.Type.Contains("image/jpeg")) // No, XPCO 3.0a can send jpeg with genericbytedata headers
                        if (bytesReceived[0] == 0xFF && bytesReceived[1] == 0xD8)
                        {
                            int neededBufSize = h.Length + 4; //Adding 4 because there is "\r\n\r\n" at the end
                            if (neededBufSize > curBufSize)
                            {
                                int newBufSize = RoundUpBufSize(neededBufSize);
                                curBufSize = newBufSize;
                                byte b0 = bytesReceived[0];
                                byte b1 = bytesReceived[1];
                                bytesReceived = new byte[curBufSize];
                                bytesReceived[0] = b0;
                                bytesReceived[1] = b1;
                            }

                            bytes = RecvFixed(networkStream, bytesReceived, 2, neededBufSize - 2);
                            bytes = Math.Abs(bytes);
                            if (bytes != neededBufSize - 2)
                            {
                                throw new Exception("Receive error B");
                            }
                        }
                        else
                        {
                            bytes = RecvFixed(networkStream, bytesReceived, 2, 30);
                            if (Math.Abs(bytes) != 30)
                            {
                                throw new Exception("Receive error C");
                            }

                            short dataType = BitConverter.ToInt16(GetReversedSubarray(bytesReceived, 0, 2), 0);
                            int length = BitConverter.ToInt32(GetReversedSubarray(bytesReceived, 2, 4), 0);
                            short codec = BitConverter.ToInt16(GetReversedSubarray(bytesReceived, 6, 2), 0);
                            short seqNo = BitConverter.ToInt16(GetReversedSubarray(bytesReceived, 8, 2), 0);
                            short flags = BitConverter.ToInt16(GetReversedSubarray(bytesReceived, 10, 2), 0);
                            long timeStampSync = BitConverter.ToInt64(GetReversedSubarray(bytesReceived, 12, 8), 0);
                            long timeStampPicture = BitConverter.ToInt64(GetReversedSubarray(bytesReceived, 20, 8), 0);
                            int reserved = BitConverter.ToInt32(GetReversedSubarray(bytesReceived, 28, 4), 0);

                            bool isKeyFrame = (flags & 0x01) == 0x01;
                            int payloadLength = length - 32;

                            if (payloadLength > curBufSize)
                            {
                                int newBufSize = RoundUpBufSize(payloadLength);
                                curBufSize = newBufSize;
                                bytesReceived = new byte[curBufSize];
                            }

                            //this appears to be the correct amount of data
                            bytes = RecvFixed(networkStream, bytesReceived, 0, payloadLength);
                            bytes = Math.Abs(bytes);
                            if (bytes != payloadLength)
                            {
                                throw new Exception("Receive error D");
                            }
                        }

                        byte[] ms = new byte[bytes];
                        Buffer.BlockCopy(bytesReceived, 0, ms, 0, bytes);
                        h.Data = ms;

                        if (OnImageReceivedMethod != null)
                        {
                            Control pi = (Control) _renderObject;
                            pi.Dispatcher.Invoke(OnImageReceivedMethod, h);
                        }

                        // Stop if we reach the end of the sequence
                        long nextTime = long.Parse(h.Next);
                        if (nextTime > PlaybackEndTime)
                            break;

                        // If there is no more video, do not keep repeating the last image, but stop the playback.
                        if (nextTime == _playbackTime)
                            break;

                        int interval =
                            (int) (nextTime -
                                   _playbackTime); // We ought to subtract also the number of milliseconds elapsed in real time since last update
                        _playbackTime = nextTime;

                        Thread.Sleep(interval / _speed);
                    }
                }

                // Tell the application I'm done
                if (OnConnectionStoppedMethod != null)
                {
                    Control pij = (Control) _renderObject;
                    pij.Dispatcher.Invoke(OnConnectionStoppedMethod, new ConnInfo(IscErrorCode.Success, ""));
                }
            }

            catch (OutOfMemoryException)
            {
                Control ipj = (Control) _renderObject;
                ipj.Dispatcher.Invoke(OnConnectionStoppedMethod, new ConnInfo(IscErrorCode.OutOfMemory, ""));
            }
            catch (IOException e)
            {
                string s = e.Message;
                Control ipj = (Control)_renderObject;
                ipj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                    new ConnInfo(IscErrorCode.SocketError, e.Message));
            }
            catch (Exception e)
            {
                Control ipj = (Control) _renderObject;
                ipj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                    new ConnInfo(IscErrorCode.InternalError, e.Message));
            }
            finally
            {
                networkStream?.Dispose();
            }
        }


        public string GetSequences(DateTime dt, int max)
        {
            Stream networkStream = null;
            try
            {
                networkStream = ConnectToImageServer();
                // Errors are handled by ConnectToImageServer
                if (networkStream == null) return "#No response";

                int maxBuf = 512 * max;

                string sendBuffer = FormatConnect();

                // Deliberately not encoded as UTF-8
                // With XPCO and XPE/WinAuth only the camera GUID and the token are used. These are always 7 bit ASCII
                // With XPE/BasicAuth, the username and password are in clear text. The server expect bytes in it's own current code page.
                // Encoding this as UTF-8 will prevent corrent authentication with other than 7-bit ASCII characters in username and password
                // Encoding this with "Default" will at least make other than 7-bit ASCII work when client's and server's code pages are alike
                // XPE's Image Server Manager has an option of manually selecting a code page.
                // But there is no way in which a client can obtain the XPE server's code page selection.
                Byte[] bytesSent = Encoding.Default.GetBytes(sendBuffer);
                networkStream.Write(bytesSent, 0, bytesSent.Length);

                Byte[] bytesReceived = new Byte[maxBuf];
                int bytes = RecvUntil(networkStream, bytesReceived, 0, maxBuf);
                string page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                bool authenticated = false;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(page);
                XmlNodeList nodes = doc.GetElementsByTagName("connected");
                foreach (XmlNode node in nodes)
                    if (node.InnerText.ToLower() == "yes")
                        authenticated = true;


                if (!authenticated)
                {
                    XmlNodeList errorNodes = doc.GetElementsByTagName("errorreason");
                    String tx = "";
                    if (errorNodes != null && errorNodes.Count > 0)
                        tx = errorNodes[0].InnerText;

                    return "#" + tx;
                }

                double centerTime = Math.Round(TimeConverter.ToDouble(dt));
                double startTime = Math.Round(TimeConverter.ToDouble(dt - TimeSpan.FromHours(24)));
                double timespan = Math.Round(centerTime - startTime);
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>1</requestid>" +
                    "<methodname>alarms</methodname>" +
                    "<centertime>{0}</centertime>" +
                    "<timespan>{1}</timespan>" +
                    "<numalarms>{2}</numalarms>" +
                    "</methodcall>\r\n\r\n", centerTime.ToString(), timespan.ToString(), max);

                bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                networkStream.Write(bytesSent, 0, bytesSent.Length);

                bytes = RecvUntil(networkStream, bytesReceived, 0, maxBuf);
                if (bytes < 0) bytes = -bytes;
                page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                return page;
            }
            catch
            {
                return "";
            }
            finally
            {
                networkStream?.Dispose();
            }
        }

        public void DoLiveCmd(string cmd)
        {
            if (_streamLive == null)
                return;

            try
            {
                Byte[] bytesSent = Encoding.UTF8.GetBytes(cmd);
                lock (_liveSocketSendLock)
                {
                    _streamLive.Write(bytesSent, 0, bytesSent.Length);
                }
            }
            catch
            {
                //Empty
            }
        }

        public void Live()
        {
            Stream networkStream = null;

            try
            {
                networkStream = ConnectToImageServer();
                // Errors are handled by ConnectToImageServer
                if (networkStream == null) return;

                _live = true;
                int maxBuf = 1024 * 8;

                string sendBuffer = FormatConnect();

                // Deliberately not encoded as UTF-8
                // With XPCO and XPE/WinAuth only the camera GUID and the token are used. These are always 7 bit ASCII
                // With XPE/BasicAuth, the username and password are in clear text. The server expect bytes in it's own current code page.
                // Encoding this as UTF-8 will prevent corrent authentication with other than 7-bit ASCII characters in username and password
                // Encoding this with "Default" will at least make other than 7-bit ASCII work when client's and server's code pages are alike
                // XPE's Image Server Manager has an option of manually selecting a code page.
                // But there is no way in which a client can obtain the XPE server's code page selection.
                Byte[] bytesSent = Encoding.Default.GetBytes(sendBuffer);

                lock (_liveSocketSendLock)
                {
                    networkStream.Write(bytesSent, 0, bytesSent.Length);
                }

                Byte[] bytesReceived = new Byte[maxBuf];

                int bytes = RecvUntil(networkStream, bytesReceived, 0, maxBuf);
                string page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                bool authenticated = false;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(page);
                XmlNodeList nodes = doc.GetElementsByTagName("connected");
                foreach (XmlNode node in nodes)
                {
                    if (node.InnerText.ToLower() == "yes")
                    {
                        authenticated = true;
                    }
                }

                if (!authenticated)
                {
                    // Tell the application I'm done
                    if (OnConnectionStoppedMethod != null)
                    {
                        Control pj = (Control)_renderObject;
                        pj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                            new ConnInfo(IscErrorCode.InvalidCredentials, ""));
                    }

                    return; // This is a thread. It won't help returning an error code
                }

                sendBuffer = FormatLive();
                bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                lock (_liveSocketSendLock)
                {
                    networkStream.Write(bytesSent, 0, bytesSent.Length);
                }

                page = Encoding.UTF8.GetString(bytesSent, 0, bytesSent.Length);

                // Others may now send on this socket, preferably using DoLiveCmd()
                _reqCounter = 2;
                _streamLive = networkStream;

                while (_live)
                {
                    // Buffer size housekeeping
                    int curBufSize = maxBuf;

                    bytes = RecvUntil(networkStream, bytesReceived, 0, maxBuf);
                    if (bytes < 0)
                    {
                        throw new Exception("Receive error A");
                    }

                    if (bytesReceived[0] == '<')
                    {
                        // This is XML status message
                        page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);
                        XmlDocument statDoc = new XmlDocument();
                        statDoc.LoadXml(page);
                        if (OnStatusItemReceivedMethod != null)
                        {
                            Control pj = (Control)_renderObject;
                            pj.Dispatcher.Invoke(OnStatusItemReceivedMethod, statDoc);
                        }

                        continue;
                    }


                    if (bytesReceived[0] == 'I')
                    {
                        // Image
                        ImageInfo h = ParseHeader(bytesReceived, 0, bytes);

                        // Takes two first bytes
                        bytes = RecvFixed(networkStream, bytesReceived, 0, 2);
                        if (2 != Math.Abs(bytes))
                        {
                            throw new Exception("Receive error 2");
                        }

                        if (bytesReceived[0] == 0xFF && bytesReceived[1] == 0xD8)
                        {
                            int neededBufSize = h.Length;
                            if (neededBufSize > curBufSize)
                            {
                                int newBufSize = RoundUpBufSize(neededBufSize);
                                curBufSize = newBufSize;
                                byte b0 = bytesReceived[0];
                                byte b1 = bytesReceived[1];
                                bytesReceived = new byte[curBufSize];
                                bytesReceived[0] = b0;
                                bytesReceived[1] = b1;
                            }

                            bytes = RecvFixed(networkStream, bytesReceived, 2, neededBufSize - 2);
                            bytes = Math.Abs(bytes);
                            if (bytes != neededBufSize - 2)
                            {
                                throw new Exception("Receive error B");
                            }
                        }
                        else
                        {
                            bytes = RecvFixed(networkStream, bytesReceived, 2, 30);
                            if (Math.Abs(bytes) != 30)
                            {
                                throw new Exception("Receive error C");
                            }

                            short dataType = BitConverter.ToInt16(GetReversedSubarray(bytesReceived, 0, 2), 0);
                            int length = BitConverter.ToInt32(GetReversedSubarray(bytesReceived, 2, 4), 0);
                            short codec = BitConverter.ToInt16(GetReversedSubarray(bytesReceived, 6, 2), 0);
                            short seqNo = BitConverter.ToInt16(GetReversedSubarray(bytesReceived, 8, 2), 0);
                            short flags = BitConverter.ToInt16(GetReversedSubarray(bytesReceived, 10, 2), 0);
                            long timeStampSync = BitConverter.ToInt64(GetReversedSubarray(bytesReceived, 12, 8), 0);
                            long timeStampPicture = BitConverter.ToInt64(GetReversedSubarray(bytesReceived, 20, 8), 0);
                            int reserved = BitConverter.ToInt32(GetReversedSubarray(bytesReceived, 28, 4), 0);

                            bool isKeyFrame = (flags & 0x01) == 0x01;
                            int payloadLength = length - 32;

                            if (payloadLength > curBufSize)
                            {
                                int newBufSize = RoundUpBufSize(payloadLength);
                                curBufSize = newBufSize;
                                bytesReceived = new byte[curBufSize];
                            }

                            //this appears to be the correct amount of data
                            bytes = RecvFixed(networkStream, bytesReceived, 0, payloadLength);
                            bytes = Math.Abs(bytes);
                            if (bytes != payloadLength)
                            {
                                throw new Exception("Receive error D");
                            }
                        }

                        LastJPEG = bytesReceived;

                        try
                        {
                            byte[] ms = new byte[bytes];
                            Buffer.BlockCopy(bytesReceived, 0, ms, 0, bytes);
                            h.Data = ms;

                            Control pi = (Control)_renderObject;
                            pi.Dispatcher.Invoke(OnImageReceivedMethod, h);
                        }
                        catch (OutOfMemoryException)
                        {
                            Control pp = (Control)_renderObject;
                            pp.Dispatcher.Invoke(OnConnectionStoppedMethod,
                                new ConnInfo(IscErrorCode.OutOfMemory, ""));
                            StopLive();
                        }
                        catch (Exception e)
                        {
                            Control pp = (Control)_renderObject;
                            pp.Dispatcher.Invoke(OnConnectionStoppedMethod,
                                new ConnInfo(IscErrorCode.NotJpegError, e.Message));
                        }
                    }
                }

                // Tell the application I'm done
                Control ipj = (Control)_renderObject;
                ipj.Dispatcher.Invoke(OnConnectionStoppedMethod, new ConnInfo(IscErrorCode.Success, ""));
            }

            catch (OutOfMemoryException)
            {
                Control ipj = (Control)_renderObject;
                ipj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                    new ConnInfo(IscErrorCode.OutOfMemory, ""));
            }
            catch (IOException e)
            {
                string s = e.Message;
                Control ipj = (Control)_renderObject;
                ipj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                    new ConnInfo(IscErrorCode.SocketError, e.Message));
            }
            catch (Exception e)
            {
                string s = e.Message;
                Control ipj = (Control)_renderObject;
                ipj.Dispatcher.Invoke(OnConnectionStoppedMethod,
                    new ConnInfo(IscErrorCode.InternalError, e.Message));
            }
            finally
            {
                networkStream?.Dispose();
            }
        }

        private byte[] GetReversedSubarray(byte[] array, int start, int length)
        {
            return array.Skip(start).Take(length).Reverse().ToArray();
        }

        private int RoundUpBufSize(int needed)
        {
            int roundup = (needed / 1024) * 1024 / 100 * 130;
            return roundup;
        }

        private static int RecvFixed(Stream stream, byte[] buf, int offset, int size)
        {
            int miss = size;
            int got = 0;
            int bytes = 0;
            int get = 1;
            int maxb = 1024 * 16;

            do
            {
                get = miss > maxb ? maxb : miss;
                bytes = stream.Read(buf, offset + got, get);
                got += bytes;
                miss -= bytes;
            } while (got < size);

            if (got > size)
            {
                throw new Exception("Buffer overflow");
            }

            if (size < 4)
                return -got;

            int i = offset + got - 4;
            if (buf[i] == '\r' && buf[i + 1] == '\n' && buf[i + 2] == '\r' && buf[i + 3] == '\n')
            {
                return got;
            }

            return -got;

        }

        private static int RecvUntil(Stream stream, byte[] buf, int offset, int size)
        {
            int miss = size;
            int got = 0;
            int bytes = 0;
            int ended = 4;
            int i = 0;

            while (got < size && ended > 0)
            {
                i = offset + got;
                bytes = stream.Read(buf, i, 1);
                if (buf[i] == '\r' || buf[i] == '\n')
                {
                    ended--;
                }
                else
                {
                    ended = 4;
                }

                got += bytes;
                miss -= bytes;
            }

            if (got > size)
            {
                throw new Exception("Buffer overflow");
            }

            if (ended == 0)
            {
                return got;
            }

            return -got;
        }

        private static ImageInfo ParseHeader(byte[] buf, int offset, int bytes)
        {
            ImageInfo h = new ImageInfo()
            {
                Length = 0,
                Type = ""
            };


            string response = Encoding.UTF8.GetString(buf, offset, bytes);
            string[] headers = response.Split('\n');
            foreach (string header in headers)
            {
                string[] keyVal = header.Split(':');
                if (keyVal[0].ToLower() == "content-length" && keyVal.Length > 1)
                {
                    h.Length = int.Parse(keyVal[1]);
                }

                if (keyVal[0].ToLower() == "content-type" && keyVal.Length > 1)
                {
                    h.Type = keyVal[1].Trim('\r').ToLower();
                }

                if (keyVal[0].ToLower() == "current" && keyVal.Length > 1)
                {
                    h.Current = keyVal[1].Trim('\r');
                }

                if (keyVal[0].ToLower() == "next" && keyVal.Length > 1)
                {
                    h.Next = keyVal[1].Trim('\r');
                }

                if (keyVal[0].ToLower() == "prev" && keyVal.Length > 1)
                {
                    h.Prev = keyVal[1].Trim('\r');
                }
            }

            return h;
        }
    }

    public class ImageInfo
    {
        public int Length;
        public string Type;
        public string Current;
        public string Next;
        public string Prev;
        public object Data;

        public ImageInfo()
        {
            Length = -1;
            Type = "";
            Current = "";
            Next = "";
            Prev = "";
            Data = null;
        }
    }

    public enum IscErrorCode
    {
        Success,
        NoConnect,
        InvalidCredentials,
        OutOfMemory,
        SocketError,
        NotJpegError,
        InternalError
    };


    public class ConnInfo
    {
        public IscErrorCode ErrorCode;
        public string Message;

        public ConnInfo(IscErrorCode errCode, string errMsg)
        {
            ErrorCode = errCode;
            Message = errMsg;
        }

        public static IPAddress ToIpv4(string dns)
        {
            IPAddress ipAddress;
            byte[] nullIp = {0, 0, 0, 0};
            try
            {
                ipAddress = IPAddress.Parse(dns);
                return ipAddress;
            }
            catch (Exception)
            {
                try
                {
                    ipAddress = null;
                    IPHostEntry ent = Dns.GetHostEntry(dns);
                    foreach (IPAddress addr in ent.AddressList)
                    {
                        if (addr.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipAddress = addr;
                            return ipAddress;
                        }
                    }

                    return new IPAddress(nullIp);
                }
                catch
                {
                    return new IPAddress(nullIp);
                }
            }
        }
    }
}
