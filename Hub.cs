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
	public class HubState
	{
		public HubState ()
		{
			Entries = new Dictionary<Uri,List<Uri>> ();
		}

		[DataMember]
		public Dictionary<Uri,List<Uri>> Entries { get; private set; }
	}

	public class HubDriver
	{
		string store_file_name = "hubstate.xml";

		public static void Main (string [] args)
		{
			if (args.Length == 0)
				Console.WriteLine ("usage: Hub.exe listen-uri");
			else
				new HubDriver ().Run (args);
		}

		void Run (string [] args)
		{
			var hub = new Hub (Load, Save);
			var host = new WebServiceHost (hub);
			host.AddServiceEndpoint (typeof (IHub), new WebHttpBinding (), args [0]);
			host.Open ();
			Console.WriteLine ("Type [CR] to close ...");
			Console.ReadLine ();
			host.Close ();
		}

		DataContractSerializer serializer = new DataContractSerializer (typeof (HubState));

		HubState Load ()
		{
			if (!File.Exists (store_file_name))
				return new HubState ();
			else
				using (var xr = XmlReader.Create (store_file_name))
					return (HubState) serializer.ReadObject (xr);
		}

		void Save (HubState state)
		{
			using (var xw = XmlWriter.Create (store_file_name))
				serializer.WriteObject (xw, state);
		}
	}

	[ServiceBehavior (InstanceContextMode = InstanceContextMode.Single)]
	public class Hub : IHub
	{
		public Hub (Func<HubState> restoreStateHandler, Action<HubState> saveStateHandler)
		{
			if (restoreStateHandler == null)
				throw new ArgumentNullException ("restoreStateHandler");
			if (saveStateHandler == null)
				throw new ArgumentNullException ("saveStateHandler");
			save_state_handler = saveStateHandler;
			state = restoreStateHandler ();
		}

		HubState state;
		Action<HubState> save_state_handler;

		public delegate void FetchPolicyCheckerDelegate (Uri topic, Uri callback);
		public event FetchPolicyCheckerDelegate FetchPolicyChecker;

		public void Subscribe (SubscriptionRequest request)
		{
			if (request == null)
				throw new ArgumentNullException ("request");
			if (request.Topic == null)
				throw new ArgumentException ("request topic is null");
			if (request.Callback == null)
				throw new ArgumentException ("request callback is null");
			if (!String.IsNullOrEmpty (request.Topic.Fragment))
				throw new ArgumentException ("request topic must not contain fragment");
			if (!String.IsNullOrEmpty (request.Callback.Fragment))
				throw new ArgumentException ("request callback must not contain fragment");

			if (request.Secret != null)
				throw new NotSupportedException ("Secure subscription is not supported yet");

			VerifyFetchPolicy (request.Topic, request.Callback);

			if (request.Verify == "sync") {
				var proxy = new WebChannelFactory<ISub> (request.Callback).CreateChannel ();
				proxy.VerifySubscribe (new VerifySubscribeRequest () {
					Mode = request.Mode,
					Topic = request.Topic,
					LeaseSeconds = request.LeaseSeconds,
					VerifyToken = request.VerifyToken });

			}
			List<Uri> list;
			if (!state.Entries.TryGetValue (request.Topic, out list))
				state.Entries [request.Topic] = list = new List<Uri> ();
			list.Add (request.Callback);

			save_state_handler (state);
		}

		void VerifyFetchPolicy (Uri topic, Uri callback)
		{
			if (FetchPolicyChecker != null)
				FetchPolicyChecker (topic, callback);
			else { // default behavior
				if (topic.Scheme != "http" && topic.Scheme != "https")
					throw new InvalidOperationException ("Only HTTP and HTTPS schemes are allowed for subscription topic and callback");
			}
		}

		public void NotifyNewContent (NotifyNewContentRequest request)
		{
			if (request == null)
				throw new ArgumentNullException ("request");
			if (request.Urls == null)
				throw new ArgumentException ("new content url is null");

			var wc = new WebClient ();
			var wc2 = new WebClient ();
			wc2.Headers ["Content-Type"] = "application/atom+xml";
			// FIXME: give actual number of subscribers (in case we handle more).
			wc.Headers ["X-Hub-Subscribers"] = "1";
			foreach (var url in request.Urls) {
				var bytes = wc.DownloadData (url);
				foreach (var callback in state.Entries [url]) {
					try {
						wc2.UploadData (callback, bytes);
					} catch (WebException ex) {
						// FIXME: log it
						Console.WriteLine ("failed to push to {0} : {1}", callback, ex);
					}
				}
			}
		}
	}
}
