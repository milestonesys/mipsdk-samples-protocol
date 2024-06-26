//
// Milestone XProtect SDK Sample Program: LoginDotNetSoap
//
// This sample only targets developers who for some reason do not want to use the Milestone .NET VideoOS.Platform.SDK.dll
//
// This source code demonstrates how you can logon to an XProtect C-code Server
// using Microsoft SOAP over HTTP.
// Logged on successfully, you obtain a token, which you can use to logon to an XProtect Recording (==Image) Server
// This second connect method uses XML over TCP according to the XProtect Image Server API
//
// The example depends on the .NET classes.
//
// You can reuse the credentials with which you are already logged on, or provide specific AD or basic credentials.
//
// If you are in another development environment, you can only use this as a source of inspiration.
//
// The example code shows the absolute minimum coding needed for a login, no more, no less, no sophisticated error handling.
// It defaults to port 80, and that the servers are always alive. All networking is synchronous.
//
// If you try connect to a server which does not exist or respond, there will be an exception.
//
// From the entire SOAP ServerCommandService protocol, the ServerCommandService classes supports the Login/Logout commands only.
// See that class for further explanation.
//

using System;
using System.Text;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using ServerCommandWrapper;

namespace LoginDotNetSoap_CS
{
    public class Program
    {
        // You must change these data to fit your environment
        private static string cam_guid = "EAC70EBF-65CA-4332-A2EE-C7FCC418D7E2";

        //Default values 
        private static string my_default_ip_management_server = "localhost";
        private static int my_default_port_management_server = 80;
        private static int my_default_port_management_server_ssl = 443;
        private static readonly Guid IntegrationId = new Guid("025047A8-170D-4334-B1FF-101B8A53F73A");
        private const string IntegrationName = "Login .Net SOAP";
        private const string Version = "1.0";

        private static int my_port_recording_server = 7563;
        // End customized data


        public static void Main(string[] args)
        {
            //First get all the user inputs: Credentials, server address, etc
            UserInput userInputs = CollectUserInput();

            //Start logging in 
            string token;
            long timeToLive;
            ServerCommandWrapper.Basic.BasicConnection serverCommandServiceBasic = null;
            ServerCommandWrapper.Ntlm.NtlmConnection serverCommandServiceNtlm = null;
            
            if (userInputs.Domain.ToLowerInvariant().Equals("basic"))
            {
                /*
                 * Establish a connection, where basic authentication is used, and log in
                 */
                serverCommandServiceBasic = new ServerCommandWrapper.Basic.BasicConnection(
                    userInputs.Username, userInputs.Password, userInputs.Hostname,
                    userInputs.Port);
                try
                {
                    token = serverCommandServiceBasic.Login(IntegrationId, Version, IntegrationName).Token;
                    timeToLive = (long) serverCommandServiceBasic.LoginInfo.TimeToLive.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to login: " + e.Message);
                    Console.WriteLine("Press any key to close application...");
                    Console.ReadKey();
                    return;
                }

            }
            else
            {
                /*
                 * Establish a connection, where Windows authentication is used, and log in
                 */
                var authenticationType = string.IsNullOrEmpty(userInputs.Username)
                    ? AuthenticationType.WindowsDefault
                    : AuthenticationType.Windows;
                serverCommandServiceNtlm = new ServerCommandWrapper.Ntlm.NtlmConnection(userInputs.Domain,
                    authenticationType, userInputs.Username, userInputs.Password,
                    userInputs.Hostname, userInputs.Port);
                try
                {
                    token = serverCommandServiceNtlm.Login(IntegrationId, Version, IntegrationName).Token;
                    timeToLive = (long) serverCommandServiceNtlm.LoginInfo.TimeToLive.TotalMilliseconds;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to login: " + ex.Message);
                    Console.WriteLine("Press any key to close application...");
                    Console.ReadKey();
                    return;
                }
            }

            // This is the token obtained
            Console.WriteLine("Token returned: {0}", token);

            // And this is how long time it is valid, make a new Login just before it expires and get a new token
            Console.WriteLine("Token is valid for: " + (timeToLive / 1000) + " seconds");


            /********************************************************
             * Now connect to a recording service or image server   *
             * ******************************************************/

            // The next 10 lines will normally be taken from the SystemInfo.xml, or GetConfiguration() result,
            // as each camera configuration contains the URL for the Recording server it is defined on.
            String imageServerName = String.Empty;
            int port = 80;

            Console.Write("Enter IP address for an XProtect Recording Server (or blank for same as Mgt Server): ");
            imageServerName = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(imageServerName))
                imageServerName = userInputs.Hostname; // Assume same as the Mgt Server is on
            port = my_port_recording_server;

            // Connect to a recording service or an image server. 
            String sendBuffer = String.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>0</requestid>" +
                "<methodname>connect</methodname><username></username><password></password>" +
                "<cameraid>{0}</cameraid><alwaysstdjpeg>no</alwaysstdjpeg>" +
                "<connectparam>id={1}&amp;connectiontoken={2}" +
                "</connectparam></methodcall>\r\n\r\n", cam_guid, cam_guid, token);
            IPHostEntry hostEntry = Dns.GetHostEntry(imageServerName);

            if (userInputs.Encryption)
                ConnectToEncryptedRecordingServer(hostEntry, sendBuffer, port);
            else
                ConnectToUnencryptedRecordingServer(hostEntry, sendBuffer, port);


            Console.WriteLine("Press any key to close application...");
            Console.ReadKey();

            if (serverCommandServiceBasic != null)
                serverCommandServiceBasic.Logout();
            if (serverCommandServiceNtlm != null)
                serverCommandServiceNtlm.Logout();
        }

        /// <summary>
        /// Use this method to connect to an encrypted recording server
        /// </summary>
        /// <param name="hostEntry">Destination of the recording server</param>
        /// <param name="sendBuffer">What to send</param>
        /// <param name="port">Port to send on</param>
        private static void ConnectToEncryptedRecordingServer(IPHostEntry hostEntry, String sendBuffer, int port)
        {
            try
            {
                StringBuilder messageData = new StringBuilder();
                String server = hostEntry.HostName;
                TcpClient client = new TcpClient(server, port);
                using (SslStream sslStream = new SslStream(client.GetStream(), false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                {
                    sslStream.AuthenticateAsClient(server);

                    sslStream.Write(Encoding.ASCII.GetBytes(sendBuffer));
                    Console.WriteLine("");
                    Console.WriteLine("Request to image server");
                    Console.WriteLine(sendBuffer);

                    int bufferLength = 256;
                    byte[] buffer = new byte[bufferLength];
                    int bytes = -1;
                    do
                    {
                        bytes = sslStream.Read(buffer, 0, buffer.Length);

                        // Use Decoder class to convert from bytes to UTF8
                        // in case a character spans two buffers.
                        Decoder decoder = Encoding.UTF8.GetDecoder();
                        char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                        decoder.GetChars(buffer, 0, bytes, chars, 0);
                        messageData.Append(chars);
                    } while (bytes == buffer.Length);

                }

                Console.WriteLine("");
                Console.WriteLine("Answer from image server");
                Console.WriteLine(messageData);

                client.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occurred: " + e.Message);
            }
        }

        /// <summary>
        /// Use this method to connect to an unencrypted recording server
        /// </summary>
        /// <param name="hostEntry">Destination of the recording server</param>
        /// <param name="sendBuffer">What to send</param>
        /// <param name="port">Port to send on</param>
        private static void ConnectToUnencryptedRecordingServer(IPHostEntry hostEntry, String sendBuffer, int port)
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = null;
            foreach (IPAddress ip in hostEntry.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) // Lets find the first IPv4 address
                {
                    ipAddress = ip;
                    break;
                }
            }
            IPEndPoint ipe = new IPEndPoint(ipAddress, port);
            sock.Connect(ipe);
            if (!sock.Connected)
            {
                Console.WriteLine("No connect");
                return;
            }


            Byte[] bytesSent = Encoding.ASCII.GetBytes(sendBuffer);
            Byte[] bytesReceived = new Byte[256];
            sock.Send(bytesSent, bytesSent.Length, 0);

            Console.WriteLine("");
            Console.WriteLine("Request to image server");
            Console.WriteLine(sendBuffer);

            try
            {
                int bytes = 0;
                string page = "";
                do
                {
                    bytes = sock.Receive(bytesReceived, 256, SocketFlags.None);
                    page = page + Encoding.ASCII.GetString(bytesReceived, 0, bytes);
                } while (sock.Available > 0);

                Console.WriteLine("");
                Console.WriteLine("Answer from image server");
                Console.WriteLine(page);

                // If the answer was OK, you may now use the traditional requests like Goto or Live
                // When done with that, you should Disconnect the SOAP Login so as to declare the token obsolete
                sock.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }


        /// <summary>
        /// How to validate the server certificate <br/>
        /// IMPORTANT: This sample accepts ANY certificate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private static bool ValidateServerCertificate(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        /// <summary>
        /// Will collect all the needed user input, like i.e. username, password, etc.
        /// </summary>
        /// <returns>All the collected user inputs</returns>
        private static UserInput CollectUserInput()
        {
            UserInput userInput = new UserInput()
            {
                Username = String.Empty,
                Password = String.Empty,
                Domain = String.Empty,
                Hostname = String.Empty,
                Port = 80,
            };

            Console.Write("Enter username or blank for default credentials: ");
            userInput.Username = Console.ReadLine();
            String pwd = null;

            if (!String.IsNullOrEmpty(userInput.Username))
            {
                String tempPass = "";
                Console.Write("Enter password: ");
                ConsoleKeyInfo keyInfo;
                while (pwd == null)
                {
                    keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (tempPass.Length > 0)
                        {
                            tempPass = tempPass.Substring(0, tempPass.Length - 1);
                            Console.Write("\rEnter password: ");
                            for (int i = 0; i < tempPass.Length; i++)
                                Console.Write("*");
                            Console.Write(" \b");
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        pwd = tempPass;
                        Console.WriteLine();

                    }
                    else
                    {
                        tempPass += keyInfo.KeyChar;
                        Console.Write("*");
                    }
                }

                userInput.Password = pwd;

                Console.Write("Enter domain (Enter \"basic\" for a basic authentication user): ");
                userInput.Domain = Console.ReadLine();                
            }
            bool isBasic = userInput.Domain.ToLowerInvariant().Equals("basic");

            Console.Write($"Enter IP address and port for the XProtect VMS [{my_default_ip_management_server}:{(isBasic ? my_default_port_management_server_ssl.ToString() : my_default_port_management_server.ToString())}] : ");                
         
            string hostname = Console.ReadLine();
            if (String.IsNullOrEmpty(hostname))
            {
                userInput.Hostname = my_default_ip_management_server;
                userInput.Port = isBasic ? my_default_port_management_server_ssl : my_default_port_management_server;
            }
            else
            {
                if (hostname.StartsWith("http://")) hostname = hostname.Substring("http://".Length);
                if (hostname.StartsWith("https://")) hostname = hostname.Substring("https://".Length);
                string[] parts = hostname.Split(':');
                if (parts.Length == 2)
                {
                    userInput.Hostname = parts[0];
                    Int32.TryParse(parts[1], out userInput.Port);
                }
                else
                {
                    Console.WriteLine("You should specify both address and port...");
                    Console.Write("Press key to exit...");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
            }

            char? encryption = null;
            Console.Write($"Does the server use encryption? [y/n] ");
            do
            {
                char key = Console.ReadKey().KeyChar;
                if (key.Equals('y') || key.Equals('n'))
                    encryption = key;
            } while (!encryption.HasValue);
            Console.WriteLine(" ");

            userInput.Encryption = encryption.Value.Equals('y') || encryption.Value.Equals('Y');
            return userInput;
        }

        /// <summary>
        /// Struct to represent the user inputs
        /// </summary>
        private struct UserInput
        {
            public String Domain;
            public String Hostname;
            public int Port;
            public String Username;
            public String Password;
            public bool Encryption;
        }
    }
}


