using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Syndication;
using System.ServiceModel.Web;
using System.Xml;

namespace Commons.PubSubHubBub
{
	[DataContract]
	public enum SubscriptionOperation
	{
		[EnumMember (Value = "subscribe")]
		Subscribe,
		[EnumMember (Value = "unsubscribe")]
		Unsubscribe,
	}

	[DataContract]
	public enum SubscriptionVerification
	{
		[EnumMember (Value = "sync")]
		Sync,
		[EnumMember (Value = "async")]
		Async,
	}

	[ServiceContract (Name = "IHub")]
	public interface IHub
	{
		[OperationContract]
		[WebInvoke (BodyStyle = WebMessageBodyStyle.Bare)]
		void Subscribe (SubscriptionRequest request);

		[OperationContract]
		[WebInvoke (BodyStyle = WebMessageBodyStyle.Bare)]
		void NotifyNewContent (NotifyNewContentRequest request);
	}

	[ServiceContract (Name = "ISub")]
	public interface ISub
	{
		[OperationContract]
		[WebGet (BodyStyle = WebMessageBodyStyle.Bare)]
		void VerifySubscribe (VerifySubscribeRequest request);

		[OperationContract]
		[WebInvoke (BodyStyle = WebMessageBodyStyle.Bare)]
		void NotifyNewContent (SyndicationFeed newFeed);
	}

	[DataContract]
	public class SubscriptionRequest
	{
		[DataMember (Name = "hub.callback", IsRequired = true)]
		public Uri Callback { get; set; }

		[DataMember (Name = "hub.mode", IsRequired = true)]
		public SubscriptionOperation Mode { get; set; }

		[DataMember (Name = "hub.topic", IsRequired = true)]
		public Uri Topic { get; set; }

		// Since hub.verify MAY include additional keywords, I don't
		// use SubscriptionVerification enumeration which will reject
		// other strings during deserialization.
		[DataMember (Name = "hub.verify", IsRequired = true)]
		public string Verify { get; set; }

		[DataMember (Name = "hub.lease_seconds")]
		public int? LeaseSeconds { get; set; }

		[DataMember (Name = "hub.secret")]
		public string Secret { get; set; }

		[DataMember (Name = "hub.verify_token")]
		public string VerifyToken { get; set; }
	}

	[DataContract]
	public class VerifySubscribeRequest
	{
		[DataMember (Name = "hub.mode", IsRequired = true)]
		public SubscriptionOperation Mode { get; set; }

		[DataMember (Name = "hub.topic", IsRequired = true)]
		public Uri Topic { get; set; }

		[DataMember (Name = "hub.challenge", IsRequired = true)]
		public string Challenge { get; set; }

		[DataMember (Name = "hub.lease_seconds")]
		public int? LeaseSeconds { get; set; }

		[DataMember (Name = "hub.verify_token")]
		public string VerifyToken { get; set; }
	}

	[DataContract]
	public class NotifyNewContentRequest
	{
		[DataMember (Name = "hub.mode", IsRequired = true)]
		const string mode = "publish";

		[DataMember (Name = "hub.url", IsRequired = true)]
		public Uri [] Urls { get; set; }
	}
}
