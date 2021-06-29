using System;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace ServerCommandWrapper.OAuth
{
	/// <summary>
	/// A message inspector that modifies the outgoing messages across a WCF client.
	/// </summary>
	public class BearerAuthorizationHeaderInspector : IClientMessageInspector
	{
		private readonly string _accessToken;
		private const HttpRequestHeader AuthorizationHeader = HttpRequestHeader.Authorization;

		public BearerAuthorizationHeaderInspector(string accessToken)
		{
			_accessToken = accessToken;
		}

		#region IClientMessageInspector Members
		/// <summary>
		/// This method adds the bearer token in <see cref="AuthorizationHeader"/> before a request message is sent to a service. 
		/// </summary>
		public object BeforeSendRequest(ref Message request, IClientChannel channel)
		{
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request), "Invalid Argument");
			}
			object httpRequestMessageObject;
			if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out httpRequestMessageObject))
			{
				var httpRequestMessage = (HttpRequestMessageProperty)httpRequestMessageObject;
				if (string.IsNullOrEmpty(httpRequestMessage.Headers[AuthorizationHeader]))
				{
					httpRequestMessage.Headers[AuthorizationHeader] = FormatToken();
				}
			}
			else
			{
				var httpRequestMessage = new HttpRequestMessageProperty();
				httpRequestMessage.Headers.Add(AuthorizationHeader, FormatToken());
				request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
			}
			return null;
		}

		/// <summary>
		/// Implement this method to inspect or modify a message after a reply message is received but
		/// prior to passing it back to the client application.
		/// </summary>
		public void AfterReceiveReply(ref Message reply, object correlationState)
		{
			//No implementation required.
		}
		#endregion
		private string FormatToken()
		{
			try
			{
				return FormattableString.Invariant($"Bearer {_accessToken}");
			}
			catch (Exception ex)
			{
				throw new CommunicationException("Failed to receive token from IDP", ex);
			}
		}
	}

	/// <summary>
	/// A SOAP behavior assisting in adding the Token to the SOAP headers.
	/// </summary>
	public class AddTokenBehavior : BehaviorExtensionElement, IEndpointBehavior
	{
		private string _accessToken;

		internal AddTokenBehavior(string accessToken)
		{
			_accessToken = accessToken;
		}

		#region IEndpointBehavior Members     
		/// <summary>
		/// Method to pass custom data at runtime to enable bindings to support custom behavior.
		/// </summary>
		public void AddBindingParameters(System.ServiceModel.Description.ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
		{
			//No implementation required.
		}

		/// <summary>
		///  Inserting the inspector into the client runtime.
		/// </summary>
		public void ApplyClientBehavior(System.ServiceModel.Description.ServiceEndpoint endpoint, ClientRuntime clientRuntime)
		{
			clientRuntime.MessageInspectors.Add(new BearerAuthorizationHeaderInspector(_accessToken));
		}

		/// <summary>
		/// Use the method to modify, examine or insert extensions to endpoint-wide execution in a service application.
		/// </summary>
		public void ApplyDispatchBehavior(System.ServiceModel.Description.ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
		{
			//No implementation required.
		}

		/// <summary>
		/// Confirms that a <see cref="ServiceEndpoint"/> meets specific requirements. This can be used to ensure that
		/// an endpoint has a certain configuration setting enabled, supports a particular feature and other requirements.
		/// </summary>
		public void Validate(System.ServiceModel.Description.ServiceEndpoint endpoint)
		{
			//No implementation required.
		}
		#endregion

		/// <summary>
		/// Gets the type of behavior.
		/// </summary>
		/// <returns>The type of behavior.</returns>
		public override Type BehaviorType
		{
			get
			{
				return typeof(AddTokenBehavior);
			}
		}

		/// <summary>
		/// Creates a behavior extension based on the current configuration settings.
		/// </summary>
		/// <returns>The behavior extension.</returns>
		protected override object CreateBehavior()
		{
			return new AddTokenBehavior(_accessToken);
		}
	}
}
