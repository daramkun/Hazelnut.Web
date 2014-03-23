using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Daramkun.Dweb.Exceptions;

namespace Daramkun.Dweb
{
	public sealed class HttpAccept : IDisposable
	{
		HttpServer server;
		Socket socket;

		public Socket Socket { get { return socket; } }

		public HttpAccept ( HttpServer server, Socket socket )
		{
			this.server = server;
			this.socket = socket;

			ReceiveRequest ();
		}

		public void ReceiveRequest ()
		{
			socket.BeginReceive ( new byte [ 0 ], 0, 0, SocketFlags.None, ( IAsyncResult ar ) =>
			{
				socket.EndReceive ( ar );
				HttpRequestHeader header;

				using ( NetworkStream networkStream = new NetworkStream ( socket, false ) )
				{
					try { header = new HttpRequestHeader ( networkStream ); }
					catch { throw new InvalidRequestException (); }
				}

				server.WriteLog ( "V{0}, [{1}] {2}", header.HttpVersion, header.RequestMethod, header.QueryString );

				// Response start
				VirtualSite virtualSite = null;
				try
				{
					if ( header.Fields.ContainsKey ( HttpRequestHeaderField.Host ) )
						if ( server.VirtualSites.ContainsKey ( header.Fields [ HttpRequestHeaderField.Host ] as string ) )
							virtualSite = server.VirtualSites [ header.Fields [ HttpRequestHeaderField.Host ] as string ];
					if ( virtualSite == null )
						virtualSite = server.VirtualSites.First ().Value;
				}
				catch
				{
					// If there is not virtual site, return error status
					HttpResponseHeader responseHeader = new HttpResponseHeader ( HttpStatusCode.ServiceUnavailable );
					SendData ( responseHeader, null );
					return;
				}

				if ( header.Fields.ContainsKey ( HttpRequestHeaderField.ContentLength ) )
				{
					int contentLength = int.Parse ( header.Fields [ HttpRequestHeaderField.ContentLength ] as string );
					if ( contentLength > virtualSite.MaximumPostSize )
					{
						HttpResponseHeader responseHeader = new HttpResponseHeader ( HttpStatusCode.RequestEntityTooLarge );
						SendData ( responseHeader, null );
						ReceiveRequest ();
						return;
					}
					if ( contentLength > 0 )
					{
						// POST data

					}
				}

				// Next data receive
				ReceiveRequest ();

				// If is redirect host then send the redirection response
				if ( virtualSite.IsRedirect )
				{
					HttpResponseHeader responseHeader = new HttpResponseHeader ( HttpStatusCode.MultipleChoices );
					responseHeader.Fields.Add ( HttpResponseHeaderField.Location, virtualSite.RootDirectory );
					SendData ( responseHeader, null );
				}
				else
				{
					// Rewrite url
					foreach ( KeyValuePair<Regex, string> k in virtualSite.RewriteRules )
					{
						if ( k.Key.IsMatch ( header.QueryString, 0 ) )
						{
							header.QueryString = k.Key.Replace ( header.QueryString, k.Value );
							break;
						}
					}

					// Get real path of url
					bool subDirectoried = false;
					HttpUrl url = new HttpUrl ( header.QueryString );
					string filename = null;
					if ( url.Path.Length > 2 )
					{
						foreach ( KeyValuePair<string, string> k in virtualSite.SubDirectory )
						{
							if ( url.Path [ 1 ] == k.Key )
							{
								filename = GetFilename ( k.Key, url, 2 );
								subDirectoried = true;
								break;
							}
						}
					}

					if ( !subDirectoried )
						filename = GetFilename ( virtualSite.RootDirectory, url, 1 );

					if ( !File.Exists ( filename ) )
					{
						string temp = filename;
						foreach ( string indexName in server.IndexNames )
						{
							if ( File.Exists ( filename + "\\" + indexName ) )
							{
								filename = filename + "\\" + indexName;
								break;
							}
						}
					}

					HttpResponseHeader responseHeader = new HttpResponseHeader ();
					Stream responseStream = null;
					ContentType fileContentType = null;
					
					if ( server.Mimes.ContainsKey ( Path.GetExtension ( filename ) ) )
						fileContentType = server.Mimes [ Path.GetExtension ( filename ) ];
					else fileContentType = new ContentType ( "text/plain" );
					bool isPluginProceed = false;

					foreach ( IPlugin plugin in server.Plugins )
					{
						if ( isPluginProceed = plugin.Run ( header, fileContentType, filename, url, out responseHeader, out responseStream ) )
							break;
					}
					if ( !isPluginProceed )
						server.OriginalPlugin.Run ( header, fileContentType, filename, url, out responseHeader, out responseStream );

					SendData ( responseHeader, responseStream );
				}
			}, null );
		}

		private string GetFilename ( string baseDirectory, HttpUrl url, int startIndex )
		{
			StringBuilder filename = new StringBuilder ();
			filename.Append ( baseDirectory );
			if ( filename [ filename.Length - 1 ] == '\\' )
				filename.Remove ( filename.Length - 1, 1 );

			for ( int i = startIndex; i < url.Path.Length; ++i )
				filename.AppendFormat ( "\\{0}", url.Path [ i ] );

			return filename.ToString ();
		}

		private void SendData ( HttpResponseHeader header, Stream stream )
		{
			if ( stream == null && server.StatusPage.ContainsKey ( header.Status ) )
			{
				stream = server.StatusPage [ header.Status ];
				stream.Position = 0;
				header.ContentType = new ContentType ( "text/html" );
				header.ContentLength = ( int ) stream.Length;
			}

			byte [] headerData = Encoding.UTF8.GetBytes ( header.ToString () );
			socket.Send ( headerData );
			if ( stream != null )
			{
				byte [] data = new byte [ 1024 ];
				while ( stream.Position != stream.Length )
				{
					socket.Send ( data, stream.Read ( data, 0, data.Length ), SocketFlags.None );
				}
			}
		}

		public void Dispose ()
		{
			socket.Disconnect ( false );
			socket.Dispose ();
		}
	}
}