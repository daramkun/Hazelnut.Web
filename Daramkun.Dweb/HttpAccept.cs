using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
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
				try
				{
					socket.EndReceive ( ar );
				}
				catch { server.SocketIsDead ( this ); }
				HttpRequestHeader header = new HttpRequestHeader ();

				using ( NetworkStream networkStream = new NetworkStream ( socket, false ) )
				{
					try { header = new HttpRequestHeader ( networkStream ); }
					catch { server.WriteLog ( "Invalid Request." ); ReceiveRequest (); return; }
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
						byte [] temp = new byte [ 1024 ];
						int length = 0;
						while ( length == contentLength )
							length += socket.Receive ( temp, contentLength - length, SocketFlags.None );
						HttpResponseHeader responseHeader = new HttpResponseHeader ( HttpStatusCode.RequestEntityTooLarge );
						SendData ( responseHeader, null );
						ReceiveRequest ();
						return;
					}
					if ( contentLength > 0 )
					{
						// POST data
						if ( header.Fields [ HttpRequestHeaderField.ContentType ] as string == "application/x-www-form-urlencoded" )
						{
							// URL Encoded POST data
							MemoryStream memoryStream = new MemoryStream ();
							byte [] temp = new byte [ 1024 ];
							int length = 0;
							while ( length < contentLength )
							{
								int len = socket.Receive ( temp, 1024, SocketFlags.None );
								length += len;
								memoryStream.Write ( temp, 0, len );
							}
							string postString = HttpUtility.UrlDecode ( memoryStream.ToArray (), 0, ( int ) memoryStream.Length, Encoding.UTF8 );
							string [] tt = postString.Split ( '&' );
							tt = tt [ 1 ].Split ( '&' );
							foreach ( string s in tt )
							{
								string [] temp2 = s.Split ( '=' );
								header.PostData.Add ( temp2 [ 0 ], ( temp2.Length == 2 ) ? HttpUtility.UrlDecode ( temp2 [ 1 ] ) : null );
							}
						}
						else
						{
							// Multipart POST data
							ContentType contentType = new ContentType ( header.Fields [ HttpRequestHeaderField.ContentType ] as string );
							using ( NetworkStream networkStream = new NetworkStream ( socket, false ) )
							{
								
							}
						}
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
						// Find sub directory
						foreach ( KeyValuePair<string, string> k in virtualSite.SubDirectory )
						{
							// If found sub directory, apply sub directory
							if ( url.Path [ 1 ] == k.Key )
							{
								filename = GetFilename ( k.Key, url, 2 );
								subDirectoried = true;
								break;
							}
						}
					}
					// If can't found sub directory, apply root directory
					if ( !subDirectoried )
						filename = GetFilename ( virtualSite.RootDirectory, url, 1 );

					// Cannot found file, apply index filename
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
					
					// Find Content-Type
					if ( server.Mimes.ContainsKey ( Path.GetExtension ( filename ) ) )
						fileContentType = server.Mimes [ Path.GetExtension ( filename ) ];
					else fileContentType = new ContentType ( "application/octet-stream" );
					// Find Plugin for send data
					bool isPluginProceed = false;
					foreach ( IPlugin plugin in server.Plugins )
					{
						if ( isPluginProceed = plugin.Run ( header, fileContentType, filename, url, out responseHeader, out responseStream ) )
							break;
					}
					// If Cannot found Plugin, Processing Original plugin
					if ( !isPluginProceed )
						server.OriginalPlugin.Run ( header, fileContentType, filename, url, out responseHeader, out responseStream );

					// Send to client
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
				stream.Dispose ();
			}
		}

		public void Dispose ()
		{
			socket.Disconnect ( false );
			socket.Dispose ();
		}
	}
}