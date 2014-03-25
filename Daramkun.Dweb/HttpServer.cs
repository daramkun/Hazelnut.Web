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
	public class HttpServer : IDisposable
	{
		Socket listenSocket;
		List<Socket> sockets;
		Dictionary<Socket, HttpAccept> clients;

		OriginalPlugin originalPlugin;

		public string ServerName { get; set; }

		public List<IPlugin> Plugins { get; private set; }
		public IPlugin OriginalPlugin { get { return originalPlugin; } }

		public Dictionary<string, ContentType> Mimes { get; private set; }
		public Dictionary<string, VirtualSite> VirtualSites { get; private set; }
		public Dictionary<HttpStatusCode, Stream> StatusPage { get; private set; }

		public TextWriter LogStream { get; set; }

		public List<string> IndexNames { get; private set; }
		public string TemporaryDirectory { get; set; }

		[Conditional ( "DEBUG" )]
		public void WriteLog ( string text, params object [] args )
		{
			if ( LogStream != null )
			{
				LogStream.Write ( "[{0:yyyy-MM-dd hh:mm:ss}][{1}] ", DateTime.Now, Thread.CurrentThread.ManagedThreadId );
				LogStream.WriteLine ( text, args );
			}
		}

		public HttpServer ( IPEndPoint endPoint, int backlog = 5, TextWriter logStream = null )
		{
			ServerName = string.Format ( "Daramkun's Dweb - the Lightweight HTTP Server/{0}", Assembly.Load ( "Daramkun.Dweb" ).GetName ().Version );

			LogStream = logStream;

			listenSocket = new Socket ( endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp );
			listenSocket.Bind ( endPoint );
			listenSocket.Listen ( backlog );

			WriteLog ( "Initialized: {0}", endPoint );

			Mimes = new Dictionary<string, ContentType> ();
			VirtualSites = new Dictionary<string, VirtualSite> ();
			Plugins = new List<IPlugin> ();
			originalPlugin = new OriginalPlugin ();

			sockets = new List<Socket> ();
			clients = new Dictionary<Socket, HttpAccept> ();

			StatusPage = new Dictionary<HttpStatusCode, Stream> ();

			TemporaryDirectory = Path.GetTempPath ();
			IndexNames = new List<string> ( new string [] { "index.html", "index.htm", "index.dhtml", "index.xhtml" } );

			Accepting ();
		}

		~HttpServer () { Dispose ( false ); }

		protected virtual void Dispose ( bool isDisposing )
		{
			if ( isDisposing )
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

		public void Dispose ()
		{
			Dispose ( true );
			GC.SuppressFinalize ( this );
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
			lock ( clients )
			{
				clients.Remove ( accept.Socket );
				sockets.Remove ( accept.Socket );
			}
			accept.Dispose ();
		}

		public bool IsServerAlive { get { return listenSocket != null; } }

		public void AddDefaultMimes ()
		{
			Mimes.Add ( ".htm", new ContentType ( "text/html" ) );
			Mimes.Add ( ".html", new ContentType ( "text/html" ) );
			Mimes.Add ( ".css", new ContentType ( "text/css" ) );
			Mimes.Add ( ".js", new ContentType ( "text/javascript" ) );
			
			Mimes.Add ( ".txt", new ContentType ( "text/plain" ) );
			Mimes.Add ( ".json", new ContentType ( "application/json" ) );
			Mimes.Add ( ".yaml", new ContentType ( "text/yaml" ) );
			Mimes.Add ( ".xml", new ContentType ( "text/xml" ) );
			Mimes.Add ( ".dtd", new ContentType ( "application/xml-dtd" ) );
			Mimes.Add ( ".xsl", new ContentType ( "application/xslt+xml" ) );
			Mimes.Add ( ".xslt", new ContentType ( "application/xslt+xml" ) );
			Mimes.Add ( ".xsd", new ContentType ( "application/xsd+xml" ) );
			Mimes.Add ( ".rss", new ContentType ( "application/rss+xml" ) );
			Mimes.Add ( ".pdf", new ContentType ( "application/pdf" ) );
			Mimes.Add ( ".md", new ContentType ( "text/x-markdown" ) );
			Mimes.Add ( ".xls", new ContentType ( "application/vnd.ms-excel" ) );
			Mimes.Add ( ".xlsx", new ContentType ( "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ) );
			Mimes.Add ( ".xltx", new ContentType ( "application/vnd.openxmlformats-officedocument.spreadsheetml.template" ) );
			Mimes.Add ( ".doc", new ContentType ( "application/msword" ) );
			Mimes.Add ( ".docx", new ContentType ( "application/vnd.openxmlformats-officedocument.wordprocessingml.document" ) );
			Mimes.Add ( ".dotx", new ContentType ( "application/vnd.openxmlformats-officedocument.wordprocessingml.template" ) );
			Mimes.Add ( ".ppt", new ContentType ( "application/vnd.ms-powerpoint" ) );
			Mimes.Add ( ".pptx", new ContentType ( "application/vnd.openxmlformats-officedocument.presentationml.presentation" ) );
			Mimes.Add ( ".potx", new ContentType ( "application/vnd.openxmlformats-officedocument.presentationml.template" ) );
			Mimes.Add ( ".ppsx", new ContentType ( "application/vnd.openxmlformats-officedocument.presentationml.slideshow" ) );

			Mimes.Add ( ".jpg", new ContentType ( "image/jpeg" ) );
			Mimes.Add ( ".jpeg", new ContentType ( "image/jpeg" ) );
			Mimes.Add ( ".png", new ContentType ( "image/png" ) );
			Mimes.Add ( ".gif", new ContentType ( "image/gif" ) );
			Mimes.Add ( ".bmp", new ContentType ( "image/bmp" ) );
			Mimes.Add ( ".dib", new ContentType ( "image/bmp" ) );
			Mimes.Add ( ".tif", new ContentType ( "image/tiff" ) );
			Mimes.Add ( ".tiff", new ContentType ( "image/tiff" ) );
			Mimes.Add ( ".ai", new ContentType ( "application/postscript" ) );
			Mimes.Add ( ".svg", new ContentType ( "image/svg+xml" ) );

			Mimes.Add ( ".zip", new ContentType ( "application/x-zip-comressed" ) );
			Mimes.Add ( ".7z", new ContentType ( "application/x-7z-comressed" ) );
			Mimes.Add ( ".rar", new ContentType ( "application/x-rar-compressed" ) );
			Mimes.Add ( ".lzh", new ContentType ( "application/x-lzh-archive" ) );
			Mimes.Add ( ".lzma", new ContentType ( "application/x-lzma-archive" ) );
			Mimes.Add ( ".tar", new ContentType ( "application/tar" ) );
			Mimes.Add ( ".bz", new ContentType ( "application/x-bzip" ) );
			Mimes.Add ( ".bz2", new ContentType ( "application/x-bzip2" ) );
			Mimes.Add ( ".gz", new ContentType ( "application/x-gzip" ) );
			Mimes.Add ( ".pkg", new ContentType ( "application/x-newton-compatible-pkg" ) );

			Mimes.Add ( ".mp3", new ContentType ( "audio/mpeg" ) );
			Mimes.Add ( ".wav", new ContentType ( "audio/wav" ) );
			Mimes.Add ( ".wave", new ContentType ( "audio/wav" ) );
			Mimes.Add ( ".ogg", new ContentType ( "audio/ogg" ) );

			Mimes.Add ( ".avi", new ContentType ( "video/x-msvideo" ) );
			Mimes.Add ( ".mp4", new ContentType ( "video/mp4" ) );
			Mimes.Add ( ".m4v", new ContentType ( "video/x-m4v" ) );
			Mimes.Add ( ".asf", new ContentType ( "video/x-ms-asf" ) );
			Mimes.Add ( ".wmv", new ContentType ( "video/x-ms-wmv" ) );
			Mimes.Add ( ".mov", new ContentType ( "video/quicktime" ) );
			Mimes.Add ( ".mkv", new ContentType ( "video/x-matroska" ) );
			Mimes.Add ( ".webm", new ContentType ( "video/webm" ) );
		}
	}
}
