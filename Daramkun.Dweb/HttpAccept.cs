using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Daramkun.Dweb.VirtualHosts;
using FieldCollection = System.Collections.Generic.Dictionary<string, string>;

namespace Daramkun.Dweb
{
	public class HttpAccept : IDisposable
	{
		Stream networkStream;
		VirtualHost virtualHost;
		Stream proxyStream;
		
		public HttpServer Server { get; private set; }
		public Socket Socket { get; private set; }

		public HttpAccept ( HttpServer server, Socket socket )
		{
			Server = server;
			Socket = socket;

			networkStream = new NetworkStream ( Socket );
			if ( Server.X509 != null )
			{
				networkStream = new SslStream ( networkStream );
				try
				{
					( networkStream as SslStream ).AuthenticateAsServer ( Server.X509 );
				}
				catch { Server.SocketIsDead ( this ); return; }
			}

			ReceiveRequest ();
		}

		~HttpAccept() { Dispose ( false ); }

		protected virtual void Dispose ( bool isDisposing )
		{
			if ( isDisposing )
			{
				if ( proxyStream != null )
					proxyStream.Dispose ();
				networkStream.Dispose ();
				try
				{
					Socket.Disconnect ( false );
				}
				catch { }
			}
		}

		public void Dispose ()
		{
			Dispose ( true );
			GC.SuppressFinalize ( this );
		}

		public void ReceiveRequest ()
		{
			Socket.BeginReceive ( new byte [ 0 ], 0, 0, SocketFlags.None, ( IAsyncResult ar ) =>
			{
				try
				{
					Socket.EndReceive ( ar );
				}
				catch { Server.SocketIsDead ( this ); return; }
				HttpRequestHeader header;// = new HttpRequestHeader ();

				try
				{
					#region Receive Request Header
					header = new HttpRequestHeader ( networkStream );
					Server.WriteLog ( "V{0}, [{1}][{2}] {3}",
						header.HttpVersion,
						header.RequestMethod,
						header.Fields.ContainsKey ( HttpHeaderField.Host ) ? header.Fields [ HttpHeaderField.Host ] : "",
						header.QueryString );
					#endregion

					#region Find Virtual Host
					// Find Virtual Host
					if ( virtualHost == null )
					{
						if ( header.Fields.ContainsKey ( HttpHeaderField.Host ) )
							if ( Server.VirtualSites.ContainsKey ( header.Fields [ HttpHeaderField.Host.Split ( ':' ) [ 0 ] ] as string ) )
								virtualHost = Server.VirtualSites [ header.Fields [ HttpHeaderField.Host.Split ( ':' ) [ 0 ] ] as string ];
						if ( virtualHost == null )
							virtualHost = Server.VirtualSites.First ().Value;
					}
					#endregion

					#region Proxy Process
					// If Virtual Host is Proxy Host, Processing the Proxy process.
					if ( virtualHost is ProxyVirtualHost )
					{
						Uri uri = new Uri ( ( virtualHost as ProxyVirtualHost ).ProxyAddress );
						if ( proxyStream == null )
						{
							IPEndPoint proxyEndPoint = null;
							
							foreach ( IPAddress address in Dns.GetHostAddresses ( uri.DnsSafeHost ) )
							{
								proxyEndPoint = new IPEndPoint ( address, uri.Port );
								break;
							}

							if ( proxyEndPoint == null )
								throw new Exception ();

							Socket proxySocket = new Socket ( proxyEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp );
							proxySocket.Connect ( proxyEndPoint );
							proxyStream = new NetworkStream ( proxySocket );
							if ( uri.Scheme == "https" )
							{
								proxyStream = new SslStream ( proxyStream );
								( proxyStream as SslStream ).AuthenticateAsClient ( ( virtualHost as ProxyVirtualHost ).ProxyAddress );
							}
						}

						if ( header.Fields.ContainsKey ( HttpHeaderField.Host ) )
							header.Fields [ HttpHeaderField.Host ] = uri.DnsSafeHost;
						
						byte [] headerBytes = Encoding.UTF8.GetBytes ( header.ToString () );
						byte [] temp = new byte [ 1024 ];
						int contentLength, length;

						proxyStream.Write ( headerBytes, 0, headerBytes.Length );
						if ( header.Fields.ContainsKey ( HttpHeaderField.ContentLength ) )
						{
							contentLength = int.Parse ( header.Fields [ HttpHeaderField.ContentLength ] as string );
							length = 0;
							while ( length != contentLength )
							{
								int len = networkStream.Read ( temp, 0, 1024 );
								length += len;
								proxyStream.Write ( temp, 0, len );
							}
						}

						HttpResponseHeader resHeader = new HttpResponseHeader ( proxyStream );
						headerBytes = Encoding.UTF8.GetBytes ( resHeader.ToString () );
						networkStream.Write ( headerBytes, 0, headerBytes.Length );

						if ( resHeader.Fields.ContainsKey ( HttpHeaderField.ContentLength ) )
							contentLength = int.Parse ( resHeader.Fields [ HttpHeaderField.ContentLength ] as string );
						else contentLength = -1;
						length = 0;
						if ( contentLength != -1 )
						{
							while ( length != contentLength )
							{
								int len = proxyStream.Read ( temp, 0, 1024 );
								length += len;
								networkStream.Write ( temp, 0, len );
							}
						}
						else
						{
							try
							{
								while ( true )
								{
									int len = proxyStream.Read ( temp, 0, 1024 );
									length += len;
									networkStream.Write ( temp, 0, len );
								}
							}
							catch ( SocketException ex ) { Server.WriteLog ( ex.ToString () ); Server.SocketIsDead ( this ); return; }
							catch { }
						}

						ReceiveRequest ();

						return;
					}
					#endregion

					#region Get POST data
					if ( header.Fields.ContainsKey ( HttpHeaderField.ContentLength ) )
					{
						int contentLength = int.Parse ( header.Fields [ HttpHeaderField.ContentLength ] as string );
						if ( contentLength > virtualHost.MaximumPostSize )
						{
							byte [] temp = new byte [ 1024 ];
							int length = 0;
							while ( length != contentLength )
								length += networkStream.Read ( temp, 0, 1024 );
							HttpResponseHeader responseHeader = new HttpResponseHeader ( HttpStatusCode.RequestEntityTooLarge );
							SendData ( responseHeader, null );
							ReceiveRequest ();
							return;
						}
						if ( contentLength > 0 )
						{
							// POST data
							if ( header.Fields [ HttpHeaderField.ContentType ] as string == "application/x-www-form-urlencoded" )
							{
								// URL Encoded POST data
								MemoryStream memoryStream = new MemoryStream ();
								byte [] temp = new byte [ 1024 ];
								int length = 0;
								while ( length < contentLength )
								{
									int len = networkStream.Read ( temp, 0, 1024 );
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
								ContentType contentType = new ContentType ( header.Fields [ HttpHeaderField.ContentType ] as string );
								ReadMultipartPOSTData ( new BinaryReader ( networkStream ), contentLength, contentType.Boundary, header.PostData );
							}
						}
					}
					#endregion

					#region Redirection
					// If is redirect host then send the redirection response
					if ( virtualHost is RedirectVirtualHost )
					{
						HttpResponseHeader responseHeader = new HttpResponseHeader ( HttpStatusCode.MultipleChoices );
						responseHeader.Fields.Add ( HttpHeaderField.Location, ( virtualHost as RedirectVirtualHost ).Redirect );
						SendData ( responseHeader, null );

						ReceiveRequest ();
						return;
					}
					#endregion

					#region Ordinary Response
					Response ( header, virtualHost as SiteVirtualHost );
					ReceiveRequest ();
					#endregion
				}
				catch ( Exception ex ) { Server.WriteLog ( ex.ToString () ); Server.SocketIsDead ( this ); return; }
			}, null );
		}

		private void ReadMultipartPOSTData ( BinaryReader reader, int contentLength, string boundary, FieldCollection dictionary )
		{
			bool partSeparatorMode = true, firstLooping = true;
			Stream tempStream = null;
			FieldCollection fields = new FieldCollection ();
			byte [] multipartData = new byte [ 4 + boundary.Length ];
			Array.Copy ( new byte [] { ( byte ) '\r', ( byte ) '\n', ( byte ) '-', ( byte ) '-' }, multipartData, 4 );
			Array.Copy ( Encoding.UTF8.GetBytes ( boundary ), 0, multipartData, 4, boundary.Length );
			reader.ReadBytes ( 2 + boundary.Length );

			while ( true )
			{
				if ( partSeparatorMode )
				{
					if ( !firstLooping )
						AddToPOST ( dictionary, fields, tempStream as MemoryStream );

					if ( Encoding.UTF8.GetString ( reader.ReadBytes ( 2 ) ) == "--" )
					{
						if ( tempStream != null ) tempStream.Dispose ();
						return;
					}
					else
					{
						string key;
						fields.Clear ();
						while ( ( key = _Utility.ReadToColon ( reader ) ) != null )
							fields.Add ( key, _Utility.ReadToNextLine ( reader ).Trim () );
						string filename = _Utility.ReadFilename ( fields [ HttpHeaderField.ContentDisposition ] );
						if ( tempStream != null ) tempStream.Dispose ();
						tempStream = ( filename == null ) ?
							new MemoryStream () as Stream :
							new FileStream (
								Path.Combine ( Server.TemporaryDirectory,
								Convert.ToBase64String ( Encoding.UTF8.GetBytes ( filename ) ) + Path.GetExtension ( filename )
							), FileMode.Create ) as Stream;
						partSeparatorMode = false;
					}
				}
				else
				{
					byte b;
					int multipartHeaderIndex = 0;
					Queue<byte> queue = new Queue<byte> ();
					while ( true )
					{
						b = reader.ReadByte ();
						if ( b == multipartData [ multipartHeaderIndex ] )
						{
							queue.Enqueue ( b );
							++multipartHeaderIndex;
							if ( multipartHeaderIndex == multipartData.Length )
							{
								partSeparatorMode = true;
								queue.Clear ();
								break;
							}
						}
						else
						{
							while ( queue.Count != 0 ) tempStream.WriteByte ( queue.Dequeue () );
							if ( b == multipartData [ 0 ] ) { queue.Enqueue ( b ); multipartHeaderIndex = 1; }
							else { tempStream.WriteByte ( b ); multipartHeaderIndex = 0; }
						}
					}
				}
				firstLooping = false;
			}
		}

		private void AddToPOST ( FieldCollection dictionary, FieldCollection fields, MemoryStream tempStream )
		{
			string filename = _Utility.ReadFilename ( fields [ HttpHeaderField.ContentDisposition ] );
			dictionary.Add ( _Utility.ReadName ( fields [ HttpHeaderField.ContentDisposition ] ), filename ?? Encoding.UTF8.GetString ( tempStream.ToArray () ) );
		}

		private void Response ( HttpRequestHeader header, SiteVirtualHost virtualHost )
		{
			// Rewrite url
			foreach ( KeyValuePair<Regex, string> k in virtualHost.RewriteRules )
			{
				if ( k.Key.IsMatch ( header.QueryString.ToString (), 0 ) )
				{
					header.QueryString = new HttpUrl ( k.Key.Replace ( header.QueryString.ToString (), k.Value ) );
					break;
				}
			}

			// Get real path of url
			bool subDirectoried = false;
			string filename = null;
			if ( header.QueryString.Path.Length > 2 )
			{
				// Find sub directory
				foreach ( KeyValuePair<string, string> k in virtualHost.SubDirectory )
				{
					// If found sub directory, apply sub directory
					if ( header.QueryString.Path [ 1 ] == k.Key )
					{
						filename = _Utility.GetFilename ( k.Key, header.QueryString, 2 );
						subDirectoried = true;
						break;
					}
				}
			}
			// If can't found sub directory, apply root directory
			if ( !subDirectoried )
				filename = _Utility.GetFilename ( virtualHost.RootDirectory, header.QueryString, 1 );

			// Cannot found file, apply index filename
			if ( !File.Exists ( filename ) )
			{
				foreach ( string indexName in Server.IndexNames )
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
			if ( Server.Mimes.ContainsKey ( Path.GetExtension ( filename ) ) )
				fileContentType = Server.Mimes [ Path.GetExtension ( filename ) ];
			else fileContentType = new ContentType ( "application/octet-stream" );
			// Find Plugin for send data
			bool isPluginProceed = false;
			PluginArgument args = new PluginArgument ()
			{
				RequestMethod = header.RequestMethod,
				Url = header.QueryString,
				Get = header.QueryString.QueryString,
				Post = header.PostData,
				ContentType = fileContentType,
				OriginalFilename = filename,
				RequestFields = header.Fields
			};
			foreach ( IPlugin plugin in Server.Plugins )
			{
				if ( isPluginProceed = plugin.Run ( args, out responseHeader, out responseStream ) )
					break;
			}
			// If Cannot found Plugin, Processing Original plugin
			if ( !isPluginProceed )
				Server.OriginalPlugin.Run ( args, out responseHeader, out responseStream );

			// Send to client
			SendData ( responseHeader, responseStream );
		}

		private void SendData ( HttpResponseHeader header, Stream stream )
		{
			if ( stream == null && Server.StatusPage.ContainsKey ( header.Status ) )
			{
				stream = Server.StatusPage [ header.Status ];
				stream.Position = 0;
				header.ContentType = new ContentType ( "text/html" );
				header.ContentLength = ( int ) stream.Length;
			}

			if ( header.Fields.ContainsKey ( HttpHeaderField.Server ) )
				header.Fields [ HttpHeaderField.Server ] = Server.ServerName;
			else header.Fields.Add ( HttpHeaderField.Server, Server.ServerName );

			byte [] headerData = Encoding.UTF8.GetBytes ( header.ToString () );
			networkStream.Write ( headerData, 0, headerData.Length );
			if ( stream != null )
			{
				byte [] data = new byte [ 1024 ];
				while ( stream.Position != stream.Length )
				{
					int len = stream.Read ( data, 0, data.Length );
					networkStream.Write ( data, 0, len );
				}
				stream.Dispose ();
			}

			networkStream.Flush ();
		}
	}
}