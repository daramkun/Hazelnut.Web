using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Daramkun.Dweb.Plugins;

namespace Daramkun.Dweb
{
	public sealed class HttpServer : IDisposable
	{
		Socket listenSocket;
		List<Socket> sockets;
		Dictionary<Socket, HttpAccept> clients;
		List<IPlugin> plugins;
		OriginalPlugin originalPlugin;
		Dictionary<string, ContentType> mimes;
		Dictionary<string, VirtualSite> virtualSites;
		Dictionary<HttpStatusCode, Stream> statusPage;

		TextWriter logStream;

		public string ServerName { get; set; }
		public List<IPlugin> Plugins { get { return plugins; } }
		public IPlugin OriginalPlugin { get { return originalPlugin; } }
		public Dictionary<string, ContentType> Mimes { get { return mimes; } }
		public Dictionary<string, VirtualSite> VirtualSites { get { return virtualSites; } }
		public TextWriter LogStream { get { return logStream; } set { logStream = value; } }
		public Dictionary<HttpStatusCode, Stream> StatusPage { get { return statusPage; } }
		public string [] IndexNames { get; set; }
		public string TemporaryDirectory { get; set; }

		[Conditional ( "DEBUG" )]
		public void WriteLog ( string text, params object [] args )
		{
			if ( logStream != null )
			{
				logStream.Write ( "[{0:yyyy-MM-dd hh:mm:ss}]", DateTime.Now );
				logStream.WriteLine ( text, args );
			}
		}

		public HttpServer ( IPEndPoint endPoint, int backlog = 5, TextWriter logStream = null )
		{
			ServerName = string.Format ( "Daramkun's Dweb - the Lightweight HTTP Server/{0}", Assembly.Load ( "Daramkun.Dweb" ).GetName ().Version );

			this.logStream = logStream;

			listenSocket = new Socket ( endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp );
			listenSocket.Bind ( endPoint );
			listenSocket.Listen ( backlog );

			WriteLog ( "Initialized: {0}", endPoint );

			mimes = new Dictionary<string, ContentType> ();
			virtualSites = new Dictionary<string, VirtualSite> ();
			plugins = new List<IPlugin> ();
			originalPlugin = new OriginalPlugin ();

			sockets = new List<Socket> ();
			clients = new Dictionary<Socket, HttpAccept> ();

			statusPage = new Dictionary<HttpStatusCode, Stream> ();

			TemporaryDirectory = Environment.GetFolderPath ( Environment.SpecialFolder.ApplicationData );
			IndexNames = new string [] { "index.html", "index.htm" };

			Accepting ();
		}

		private void Accepting ()
		{
			WriteLog ( "Accept Ready" );
			listenSocket.BeginAccept ( ( IAsyncResult ar ) =>
			{
				Socket socket = listenSocket.EndAccept ( ar );
				HttpAccept accept = new HttpAccept ( this, socket );
				clients.Add ( socket, accept );
				WriteLog ( "Accepted: {0}", accept.Socket.RemoteEndPoint );
				Accepting ();
			}, null );
		}

		public void SocketIsDead ( HttpAccept accept )
		{
			clients.Remove ( accept.Socket );
			sockets.Remove ( accept.Socket );
		}

		public void AddPlugin ( IPlugin plugin ) { plugins.Add ( plugin ); }
		public void RemovePlugin ( IPlugin plugin ) { plugins.Remove ( plugin ); }

		public bool IsServerAlive { get { return listenSocket != null; } }

		public void AddDefaultMimes ()
		{
			ContentType webMime = new ContentType ( "text/html" );
			mimes.Add ( ".htm", webMime );
			mimes.Add ( ".html", webMime );

			mimes.Add ( ".jpg", new ContentType ( "image/jpeg" ) );
			mimes.Add ( ".jpeg", new ContentType ( "image/jpeg" ) );
			mimes.Add ( ".png", new ContentType ( "image/png" ) );
			mimes.Add ( ".gif", new ContentType ( "image/gif" ) );
			mimes.Add ( ".bmp", new ContentType ( "image/bmp" ) );
			mimes.Add ( ".dib", new ContentType ( "image/bmp" ) );
			mimes.Add ( ".tif", new ContentType ( "image/tiff" ) );
			mimes.Add ( ".tiff", new ContentType ( "image/tiff" ) );

			mimes.Add ( ".zip", new ContentType ( "application/x-zip-comressed" ) );
			mimes.Add ( ".7z", new ContentType ( "application/x-7z-comressed" ) );
			mimes.Add ( ".rar", new ContentType ( "application/octet-stream" ) );

			mimes.Add ( ".mp3", new ContentType ( "audio/mpeg" ) );
			mimes.Add ( ".wav", new ContentType ( "audio/wav" ) );
			mimes.Add ( ".wave", new ContentType ( "audio/wav" ) );
			mimes.Add ( ".ogg", new ContentType ( "audio/ogg" ) );

			mimes.Add ( ".avi", new ContentType ( "video/x-msvideo" ) );
			mimes.Add ( ".mp4", new ContentType ( "video/mp4" ) );
			mimes.Add ( ".m4v", new ContentType ( "video/x-m4v" ) );
			mimes.Add ( ".asf", new ContentType ( "video/x-ms-asf" ) );
			mimes.Add ( ".wmv", new ContentType ( "video/x-ms-wmv" ) );
		}

		public void Dispose ()
		{
			foreach ( KeyValuePair<Socket, HttpAccept> accept in clients )
				accept.Value.Dispose ();
			clients.Clear ();
			sockets.Clear ();
			listenSocket.Disconnect ( false );
			listenSocket.Dispose ();
			listenSocket = null;
		}
	}
}
