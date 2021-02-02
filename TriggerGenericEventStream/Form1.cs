using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace TriggerGenericEventStream
{
    public partial class Form1 : Form
    {
        private Socket socket;
        public delegate void EchoReceivedEventHandler(object sender, EchoReceivedEventArgs e);
        public event EchoReceivedEventHandler EchoReceivedEvent;

        public Form1()
        {
            InitializeComponent();
            this.EchoReceivedEvent += new EchoReceivedEventHandler(Form1_EchoReceivedEvent);
        }

        private delegate void AddEchoCallback(string echo);
        void Form1_EchoReceivedEvent(object sender, EchoReceivedEventArgs e)
        {
            textBoxEcho.BeginInvoke(new AddEchoCallback(AddEcho), e.Echo);
        }

        private void AddError(string err)
        {
            AddEcho("Local: " + err);
        }

        private void AddEcho(string echo)
        {
            if (echo.EndsWith("\r\n"))
                textBoxEcho.Text += (echo);
            else
                textBoxEcho.Text += (echo + "\r\n");
            textBoxEcho.SelectionStart = textBoxEcho.Text.Length;
            textBoxEcho.ScrollToCaret();
        }
        private void buttonSend_Click(object sender, EventArgs ea)
        {
            IPAddress ipaddr;
            IPEndPoint ipe;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                ipaddr = IPAddress.Parse(textBoxAddress.Text);
                ipe = new IPEndPoint(ipaddr, 1235);
            }
            catch (Exception e)
            {
                AddEcho(e.Message);
                return;
            }

            try
            {
                socket.Connect(ipe);
            }
            catch (Exception e)
            {
                AddEcho(e.Message);
                return;
            }

            // Here is a very small "endless" stream...
            Send(Encoding.UTF8.GetBytes(textBox1.Text));
            Send(Encoding.UTF8.GetBytes(textBox2.Text));
            Send(Encoding.UTF8.GetBytes(textBox3.Text));
            
            socket.Shutdown(SocketShutdown.Send); 
        }

        private void Send(byte[] bytes)
        {
            int n = bytes.Length + 2;
            byte[] bytesToSend = new byte[n];
            Array.Copy(bytes, 0, bytesToSend, 0, bytes.Length);
            bytesToSend[bytes.Length] = 13;
            bytesToSend[bytes.Length + 1] = 10;
            try
            {           
                n = socket.Send(bytesToSend, bytesToSend.Length, 0);
                ReceiveCallback();
            }
            catch (Exception e)
            {
                AddEcho(e.Message);
            }
        }

        private void ReceiveCallback()
        {
            string result = string.Empty;
            
            try
            {
                byte[] b = new byte[100];
                int k = socket.Receive(b);
                result = Encoding.UTF8.GetString(b,0,k);
            }
            catch (Exception e)
            {
                AddEcho(e.Message);
            }

            if (EchoReceivedEvent != null && !string.IsNullOrEmpty(result))
                EchoReceivedEvent(this, new EchoReceivedEventArgs(result));
        }
    }
    public class EchoReceivedEventArgs
    {
        public EchoReceivedEventArgs(string s) { Echo = s; }
        public String Echo { get; private set; }
    }

}
