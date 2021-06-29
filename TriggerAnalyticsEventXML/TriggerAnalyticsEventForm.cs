using System;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TriggerAnalyticsEventXML
{
	public partial class TriggerAnalyticsEventForm : Form
	{
		public TriggerAnalyticsEventForm()
		{
			InitializeComponent();
		}
        #region user click handling

        private void btnInsertAnalyticsEventXML_Click(object sender, EventArgs e)
		{
			string analyticsXml = GetAnalyticsXML(chkIncludeOverlay.Checked);
			txtAnalyticsXML.Text = analyticsXml;
		}

        private void btnSendXML_Click(object sender, EventArgs e)
        {
            //Replace timestamp token with current time
            lblResponse.Text = "Socket response";
            string analyticsXml = txtAnalyticsXML.Text;
            analyticsXml = analyticsXml.Replace("$timestamp$", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"));
            // Sending the XML
            // Note the response is important, you can see whether the AnalyticsEvent was successfully received
            string response = SendXmlWithSocket(analyticsXml, txtDestinationAddress.Text, Convert.ToInt32(txtDestinationPort.Text));
            txtResponse.Text = response;
        }

        private void btnValidateXML_Click(object sender, EventArgs e)
        {
            lblResponse.Text = "Validation Result";
            txtResponse.Text = "";
            btnSendXML.Enabled = ValidateTextbox();
        }

        private void txtAnalyticsXML_TextChanged(object sender, EventArgs e)
        {
            btnSendXML.Enabled = true;
            txtResponse.Text = "";
        }
        #endregion

        #region private methods

        bool ValidateTextbox()
        {
            bool success = true;
            XmlDocument xmlCopy = new XmlDocument();
            xmlCopy.Schemas.Add("urn:milestone-systems", "AnalyticsEvent.xsd"); // AnalyticsEvent XSD
            xmlCopy.Schemas.Add("http://tempuri.org/Alert.xsd", "Alert.xsd");   // Alert XSD put in the sample enable the sending of this older format
            string analyticsXml = txtAnalyticsXML.Text;
            analyticsXml = analyticsXml.Replace("$timestamp$", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"));
            try
            {            
               xmlCopy.LoadXml(analyticsXml);
               xmlCopy.Validate(null);  // if wanting to use the ValXMLEventHandler put it as parameter instead of null

            }
            catch (Exception  e)
            {
                txtResponse.Text = e.Message.ToString();
                success = false;
            }

            if (success)
            {
                txtResponse.Text = "Successfully validated the XML against the schemas.";
            }
            return success;
        }
        #endregion

        #region XML Generation

        private string GetAnalyticsXML(bool includeOverlay)
        {
            XmlDocument xmlDoc = new XmlDocument();

            if (includeOverlay)
            {
                xmlDoc.Load("AnalyticsEventOverlay.xml");
            }
            else
            {
                xmlDoc.Load("AnalyticsEvent.xml");
            }
            return Beautify(xmlDoc);
        }


		static public string Beautify(XmlDocument xmlDoc)
		{
			StringBuilder sb = new StringBuilder();
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Indent = true;
			settings.IndentChars = "  ";
			settings.NewLineChars = "\r\n";
			settings.NewLineHandling = NewLineHandling.Replace;
			settings.OmitXmlDeclaration = true;
			using (XmlWriter writer = XmlWriter.Create(sb, settings))
			{
				xmlDoc.Save(writer);
			}
			return sb.ToString();
		}

        #endregion

        #region Protocol handling

        private static string SendXmlWithSocket(string xmlMessage, string hostname, int port)
		{
			byte[] receiveData = new byte[1024];
			int length;
			using (Socket socket = ConnectSocket(hostname, port))
			{
                if (socket == null)
                {
                    return "Unable to contact server";
                }
				socket.SendTimeout = 10000;
				socket.ReceiveTimeout = 10000;

				// Add HTTP header to Analytics event XML
				string data = string.Format(
					"POST / HTTP/1.1\r\n" +
					"Content-Type: text/xml\r\n" +
					"Content-Length: {0}\r\n" +
					"Connection: Keep-Alive\r\n\r\n{1}",
					Encoding.UTF8.GetByteCount(xmlMessage), xmlMessage);

				// Send XML
				try
				{
					length = socket.Send(Encoding.UTF8.GetBytes(data));
				}
				catch (SocketException e)
				{
					string msg = string.Format("SocketException ({0}): {1}", e.SocketErrorCode, e.Message);
					if (e.InnerException != null)
					{
						msg += Environment.NewLine + "InnerException: " + e.InnerException.Message;
						//MessageBox.Show("Error sending analytics event to event server: " + msg);
					}
					return "Error sending analytics event to event server: " + msg;
				}

				try
				{
					length = socket.Receive(receiveData);
				}
				catch (SocketException e)
				{
					string msg = string.Format("SocketException ({0}): {1}", e.SocketErrorCode, e.Message);
					if (e.InnerException != null)
					{
						msg += Environment.NewLine + "InnerException: " + e.InnerException.Message;
					}
					//MessageBox.Show("Error receiving reply from event server: " + msg);
					return "Error receiving reply from event server: " + msg;
				}
			}

			// Check response
			string contentsSeparator = "\r\n\r\n";
			string response = Encoding.UTF8.GetString(receiveData, 0, length);
			Match headerMatch = Regex.Match(response, @"^HTTP/(?<version>[\d\.]+) (?<code>\d+) (?<message>[\w\s]+)\r\n");
			Match contentMatch = Regex.Match(response, @"^Content-Length: (?<length>\d+)\r\n", RegexOptions.Multiline);
			int responseIdx = response.IndexOf(contentsSeparator);

			if (!headerMatch.Success || !contentMatch.Success || responseIdx == -1)
			{
				return "Unknown response: " + response;
			}

			int contentLength = Int32.Parse(contentMatch.Groups["length"].Value);
			int returnCode = Int32.Parse(headerMatch.Groups["code"].Value);
			string message = headerMatch.Groups["message"].Value;
			string content = response.Substring(responseIdx + contentsSeparator.Length, contentLength);

			switch (returnCode)
			{
				case 200:
					// Fail on unknown event message. Ignore all other warnings...
					if (content.Contains("Warning: Event message not known"))
					{
					}
					else
					{
					}
					break;
				case 400:
					break;
				case 403:
					break;
				case 500:
					break;
				default:
					break;
			}

			Debug.WriteLine("Socket receive: " + response);
			return response;
		}

		private static Socket ConnectSocket(string server, int port)
		{
			Socket s = null;
			IPHostEntry hostEntry = null;

			// Get host related information.
			hostEntry = Dns.GetHostEntry(server);
            // Loop through the AddressList to obtain the supported AddressFamily. This is to avoid 
			// an exception that occurs when the host IP Address is not compatible with the address family 
			// (typical in the IPv6 case). 
			foreach (IPAddress address in hostEntry.AddressList)
			{
				IPEndPoint ipe = new IPEndPoint(address, port);
                Socket tempSocket =
					new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			    try
			    {
			        tempSocket.Connect(ipe);

			        if (tempSocket.Connected)
			        {
			            s = tempSocket;
			            break;
			        }
			        else
			        {
			            continue;
			        }
			    }
			    catch (Exception ex)
			    {
                    MessageBox.Show($"Failed to connect to socket: {server}. Reason: {ex.Message}");
                    return null;
                }
            }
			return s;
        }
        #endregion

    }
}
