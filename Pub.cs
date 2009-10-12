using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Syndication;
using System.ServiceModel.Web;
using System.Xml;

namespace Commons.PubSubHubBub
{
	// stores all atom feeds in order
	public class PubState : List<FeedState>
	{
	}

	public class FeedState
	{
		public FeedState ()
		{
		}

		public FeedState (string feed)
		{
			Feed = feed;
		}

		public string Feed { get; set; }
		public int RefCount { get; set; }
	}

	public class PubDriver
	{
		string store_file_name = "pubstate.xml";

		public static void Main (string [] args)
		{
			if (args.Length < 2)
				Console.WriteLine ("usage: Pub.exe hub-uri listen-uri");
			else
				new PubDriver ().Run (args);
		}

		void Run (string [] args)
		{
			var pub = new Pub (new Uri (args [0]), Load, Save);
			var host = new WebServiceHost (pub);
			host.AddServiceEndpoint (typeof (IPub), new WebHttpBinding (), args [1]);
			host.Open ();
			while (true) {
				Console.WriteLine ("Enter file name to publish as a feed, or empty to quit ...");
				var l = Console.ReadLine ();
				if (String.IsNullOrEmpty (l))
					break;
				var s = File.ReadAllText (l);
				pub.AddFeeds (s);
			}
			host.Close ();
		}

		DataContractSerializer serializer = new DataContractSerializer (typeof (PubState));

		PubState Load ()
		{
			if (!File.Exists (store_file_name))
				return new PubState ();
			else
				using (var xr = XmlReader.Create (store_file_name))
					return (PubState) serializer.ReadObject (xr);
		}

		void Save (PubState state)
		{
			using (var xw = XmlWriter.Create (store_file_name))
				serializer.WriteObject (xw, state);
		}
	}

	// Since it is rather a hack for publisher contract, I didn't add it
	// to the common contract definition source.
	[ServiceContract (Name = "IPub")]
	public interface IPub
	{
		[OperationContract]
		[WebGet (UriTemplate = "/feed/{id}")]
		SyndicationFeed GetFeed (int id);
	}

	public class Pub
	{
		public Pub (Uri hubAddress, Func<PubState> restoreStateHandler, Action<PubState> saveStateHandler)
		{
			if (hubAddress == null)
				throw new ArgumentNullException ("hubAddress");
			if (restoreStateHandler == null)
				throw new ArgumentNullException ("restoreStateHandler");
			if (saveStateHandler == null)
				throw new ArgumentNullException ("saveStateHandler");
			state = (PubState) restoreStateHandler ();
			save_state_handler = saveStateHandler;
			hub_address = hubAddress;
		}

		Uri hub_address;
		Action<PubState> save_state_handler;
		PubState state;

		public void AddFeeds (params string [] feeds)
		{
			Uri baseUri = OperationContext.Current.Channel.LocalAddress.Uri;
			int start = state.Count;
			foreach (var feed in feeds)
				state.Add (new FeedState (feed));
			var hub = new WebChannelFactory<IHub> (hub_address).CreateChannel ();
			var urls = new Uri [feeds.Length];
			for (int i = 0; i < urls.Length; i++) {
				// verify if the content is readable.
				SyndicationFeed.Load (XmlReader.Create (new StringReader (feeds [i])));

				urls [i] = new Uri (baseUri, "feed/" + (start + i));
			}

			hub.NotifyNewContent (new NotifyNewContentRequest () { Urls = urls.ToArray () });

			save_state_handler (state);
		}

		public SyndicationFeed GetFeed (int id)
		{
			int subs;
			if (int.TryParse (WebOperationContext.Current.IncomingRequest.Headers ["X-Hub-Subscribers"], out subs))
				state [id].RefCount += subs;
			return SyndicationFeed.Load (XmlReader.Create (new StringReader (state [id].Feed)));
		}
	}
}
