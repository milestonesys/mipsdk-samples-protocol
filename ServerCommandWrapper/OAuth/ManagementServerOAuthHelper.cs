using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Text;
using System.Xml;


namespace ServerCommandWrapper.OAuth
{
	/// <summary>
	/// Methods helping establish connection to the VMS Management Server via WCF
	/// </summary>
	public class ManagementServerOAuthHelper
	{
		private const string CommandServiceOAuthPath = "ManagementServer/ServerCommandServiceOAuth.svc";

		/// <summary>
		/// Get the correct binding for communicating with the Management Server
		/// </summary>
		/// <param name="isHttps">If it is OAuthSecure or OAuthUnsecure</param>
		/// <returns>A binding that can be used to communicate with the Management Server</returns>
		public static Binding GetOAuthBinding(bool isHttps)
		{
			var binding = new BasicHttpBinding();
			binding.Security.Mode = isHttps ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None;
			binding.ReaderQuotas.MaxStringContentLength = 2147483647;
			binding.MaxReceivedMessageSize = 2147483647;
			binding.MaxBufferPoolSize = 2147483647;
			if (!Debugger.IsAttached)
			{
				// Avoid timeout if debugging calls to server
				binding.ReceiveTimeout = TimeSpan.FromMinutes(10);
				binding.SendTimeout = TimeSpan.FromMinutes(10);
				binding.CloseTimeout = TimeSpan.FromMinutes(10);
			}
			binding.BypassProxyOnLocal = false;
			binding.HostNameComparisonMode = HostNameComparisonMode.StrongWildcard;
			binding.MessageEncoding = WSMessageEncoding.Text;
			binding.TextEncoding = Encoding.UTF8;
			binding.UseDefaultWebProxy = true;
			binding.AllowCookies = false;

			binding.ReaderQuotas = XmlDictionaryReaderQuotas.Max;

			return binding;
		}

		/// <summary>
		/// Configures <see cref="ServiceEndpoint"/> setting all the required fields and behaviors 
		/// </summary>
		/// <param name="serviceEndpoint">The ServiceEndpoint to be configured</param>
		public static void ConfigureEndpoint(ServiceEndpoint serviceEndpoint)
		{
			if (serviceEndpoint == null)
			{
				throw new ArgumentNullException(nameof(serviceEndpoint));
			}

			foreach (OperationDescription operationDescription in serviceEndpoint.Contract.Operations)
			{
				IOperationBehavior operationBehavior = operationDescription.Behaviors[typeof(DataContractSerializerOperationBehavior)];
				if (operationBehavior != null)
				{
					DataContractSerializerOperationBehavior dataContractSerializerOperationBehavior = operationBehavior as DataContractSerializerOperationBehavior;
					if (dataContractSerializerOperationBehavior != null)
					{
						dataContractSerializerOperationBehavior.MaxItemsInObjectGraph = 2147483647;
					}
				}
			}
		}

        /// <summary>
        /// Generates Uri based on host name and port.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="port"></param>
        /// <param name="prefix"></param>
        /// <returns>A URI that can be used to create an endpoint.</returns>
        public static Uri CalculateServiceUrl(string hostName, int port, string prefix)
		{
			var baseUri = new UriBuilder(prefix == "https" ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, hostName, port).Uri;
			return new Uri(baseUri, CommandServiceOAuthPath);
		}
	}
}
