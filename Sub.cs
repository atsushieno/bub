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
	public class SubState : List<Subscription>
	{
	}

	public class Subscription
	{
		public Subscription (Uri topic, Uri callback)
		{
			Topic = topic;
			Callback = callback;
		}

		public Uri Callback { get; set; }
		public Uri Topic { get; set; }
	}

	public class SubDriver
	{
		string store_file_name = "substate.xml";

		public static void Main (string [] args)
		{
			if (args.Length < 2)
				Console.WriteLine ("usage: Sub.exe hub-uri listen-uri");
			else
				new SubDriver ().Run (args);
		}

		void Run (string [] args)
		{
			var sub = new Sub (new Uri (args [0]), NewFeedArrived, Load, Save);
			var host = new WebServiceHost (sub);
			host.AddServiceEndpoint (typeof (ISub), new WebHttpBinding (), args [1]);
			host.Open ();
			Console.WriteLine ("Type [CR] to close ...");
			Console.ReadLine ();
			host.Close ();
		}

		DataContractSerializer serializer = new DataContractSerializer (typeof (SubState));

		void NewFeedArrived (SyndicationFeed feed)
		{
			Console.WriteLine ("New feed: {0} {1} {2}", feed.Id, feed.Authors.First (), feed.Title);
		}

		SubState Load ()
		{
			if (!File.Exists (store_file_name))
				return new SubState ();
			else
				using (var xr = XmlReader.Create (store_file_name))
					return (SubState) serializer.ReadObject (xr);
		}

		void Save (SubState state)
		{
			using (var xw = XmlWriter.Create (store_file_name))
				serializer.WriteObject (xw, state);
		}
	}

	[ServiceBehavior (InstanceContextMode = InstanceContextMode.Single)]
	public class Sub : ISub
	{
		public Sub (Uri hubAddress, Action<SyndicationFeed> newFeedHandler, Func<SubState> restoreStateHandler, Action<SubState> saveStateHandler)
		{
			if (hubAddress == null)
				throw new ArgumentNullException ("hubAddress");
			if (newFeedHandler == null)
				throw new ArgumentNullException ("newFeedHandler");
			if (restoreStateHandler == null)
				throw new ArgumentNullException ("restoreStateHandler");
			if (saveStateHandler == null)
				throw new ArgumentNullException ("saveStateHandler");

			hub_address = hubAddress;
			save_state_handler = saveStateHandler;
			state = restoreStateHandler ();
			new_feed_handler = newFeedHandler;
		}

		Action<SubState> save_state_handler;
		Action<SyndicationFeed> new_feed_handler;

		Uri hub_address;
		SubState state;
		string secret = null; // not supported yet

		public int? LeaseSeconds { get; set; }

		// client request support
		public void ProcessSubscrptionRequest (Uri topic, SubscriptionOperation mode)
		{
			if (topic == null)
				throw new ArgumentNullException ("topic");

			var cb = OperationContext.Current.Channel.LocalAddress.Uri;
			var r = new SubscriptionRequest () {
				Callback = cb,
				Mode = mode,
				Topic = topic,
				Verify = "async",
				Secret = secret
				};
			if (LeaseSeconds != null)
				r.LeaseSeconds = LeaseSeconds;

			var hub = new WebChannelFactory<IHub> (hub_address).CreateChannel ();
			hub.Subscribe (r);

			state.Add (new Subscription (cb, topic));
			save_state_handler (state);
		}

		// ISub implementation
		public void VerifySubscribe (VerifySubscribeRequest request)
		{
			if (request == null)
				throw new ArgumentNullException ("request");
			if (request.Topic == null)
				throw new ArgumentException ("request topic is null");
			if (!String.IsNullOrEmpty (request.Topic.Fragment))
				throw new ArgumentException ("request topic must not contain fragment");

			// FIXME: retrieve user info from callback Uri (throws exception if it is not found)
			state.First (s => s.Topic.Equals (request.Topic));
		}

		public void NotifyNewContent (SyndicationFeed feed)
		{
			new_feed_handler (feed);
		}
	}
}
