using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

// Be aware that with this command no authentication is possible.
// Please consider alternative ways of communicating, like triggering User-Defined event with camera id's as parameters.
// Also on the XProtect Advanced product family you have to enable the functionality in the Options dialog of the Management Client before using it.

namespace TriggerGenericEvent
{
    class Program
    {
        enum ParmType { None, Host, Event, Port };

        static void WriteUsage()
        {
            Console.WriteLine("Usage: TriggerGenericEvent [[-i] IP address][[-e] event][[-p] port][-u][-6]");
            Console.WriteLine("Ex:    TriggerGenericEvent 10.0.0.1 \"open door\" 8080");
            Console.WriteLine("Ex:    TriggerGenericEvent -e \"open door\" -i 10.0.0.1 -p 8080");
            Console.WriteLine("Ex:    TriggerGenericEvent -i 192.168.10.12 -p 8080 -e start_sirene -u");
            Console.WriteLine("Switches:");
            Console.WriteLine("-i: Next parameter is IP address");
            Console.WriteLine("-e: Next parameter is generic event string");
            Console.WriteLine("-p: Next parameter is IP port");
            Console.WriteLine("-u: Use UDP");
            Console.WriteLine("-6: Use IPv6");
        }

        static void Main(string[] args)
        {
        	HandleCommand(args);
        	Console.WriteLine("");
        	Console.WriteLine("Press any key");
        	Console.ReadKey();
        }

		static void HandleCommand(string[] args)
		{
    		int iport = 1235;                       // Can also be 1234 
            string hostname = "localhost";
            string toSend = "myevent";
            ProtocolType ptype = ProtocolType.Tcp;
            SocketType stype = SocketType.Stream;
            AddressFamily afam = AddressFamily.InterNetwork;
            ParmType parmtype = ParmType.Host;

            if (args.Length < 2)
            {
                WriteUsage();
                return;
            }

            foreach (string parm in args)
            {
                if (parm.StartsWith("-"))
                {
                    switch(parm[1])
                    {
                        case '?':
                            WriteUsage();
                            return;

                        case 'i':
                        case 'I':
                            parmtype = ParmType.Host;
                            break;

                        case 'e':
                        case 'E':
                            parmtype = ParmType.Event;
                            break;

                        case 'p':
                        case 'P':
                            parmtype = ParmType.Port;
                            break;

                        case 'u':
                        case 'U':
                            ptype = ProtocolType.Udp;
                            stype = SocketType.Dgram;
                            parmtype = ParmType.None;
                            break;

                        case '6':
                            afam = AddressFamily.InterNetworkV6;
                            parmtype = ParmType.None;
                            break;

                        default:
                            parmtype = ParmType.None;
                            break;
                    }
                }
                else
                {
                    switch (parmtype)
                    {
                        case ParmType.Host:
                            hostname = parm;
                            parmtype = ParmType.Event;
                            break;

                        case ParmType.Port:
                            iport = int.Parse(parm);
                            parmtype = ParmType.None;
                            break;

                        case ParmType.Event:
                            toSend = parm;
                            parmtype = ParmType.Port;
                            break;

                        case ParmType.None:
                        default:
                    		Console.WriteLine("Parameter: '" + parm + "' is not understood");
                            break;
                    }                 
                }
            }

            string sptype = ptype.ToString();
            Console.WriteLine("Sending \"{0}\" to {1} at port {2} using {3} on {4}", toSend, hostname, iport, sptype.ToUpper(), afam);

            Socket sock;
            IPAddress ipaddr;
            IPEndPoint ipe;
            try
            {
                sock = new Socket(afam, stype, ptype);
                ipaddr = IPAddress.Parse(hostname);
                ipe = new IPEndPoint(ipaddr, iport);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            try
            {
                sock.Connect(ipe);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
 
            int n = 0;
            Byte[] bytesSent = Encoding.ASCII.GetBytes(toSend);
            try
            {
                n = sock.Send(bytesSent, bytesSent.Length, 0);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                sock.Close();
                return;
            }

            if (n == bytesSent.Length)
            {
                Console.WriteLine("String sent to specified server & port.");
            }
            else
            {
                Console.WriteLine("Send error.");
            }

            sock.Close();
        }
    }
}
