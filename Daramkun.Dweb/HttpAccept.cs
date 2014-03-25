using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

using FieldCollection = System.Collections.Generic.Dictionary<string, string>;

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
				catch { server.SocketIsDead ( this ); return; }
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
					if ( header.Fields.ContainsKey ( HttpHeaderField.Host ) )
						if ( server.VirtualSites.ContainsKey ( header.Fields [ HttpHeaderField.Host ] as string ) )
							virtualSite = server.VirtualSites [ header.Fields [ HttpHeaderField.Host ] as string ];
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

				if ( header.Fields.ContainsKey ( HttpHeaderField.ContentLength ) )
				{
					int contentLength = int.Parse ( header.Fields [ HttpHeaderField.ContentLength ] as string );
					if ( contentLength > virtualSite.MaximumPostSize )
					{
						byte [] temp = new byte [ 1024 ];
						int length = 0;
						while ( length == contentLength )
							length += socket.Receive ( temp, contentLength - length >= 1024 ? 1024 : contentLength - length, SocketFlags.None );
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
							try
							{
								while ( length < contentLength )
								{
									int len = socket.Receive ( temp, 1024, SocketFlags.None );
									length += len;
									memoryStream.Write ( temp, 0, len );
								}
							}
							catch { server.SocketIsDead ( this ); return; }

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
							using ( NetworkStream networkStream = new NetworkStream ( socket, false ) )
							{
								try
								{
									ReadMultipartPOSTData ( new BinaryReader ( networkStream ), contentLength, contentType.Boundary, header.PostData );
								}
								catch { server.SocketIsDead ( this ); return; }
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
					responseHeader.Fields.Add ( HttpHeaderField.Location, virtualSite.RootDirectory );
					SendData ( responseHeader, null );
				}
				else
				{
					Response ( header, virtualSite );
				}
			}, null );
		}

		private void ReadMultipartPOSTData ( BinaryReader reader, int contentLength, string boundary, FieldCollection dictionary )
		{
			bool partSeparatorMode = true, firstLooping = true;
			Stream tempStream = null;
			FieldCollection fields = new FieldCollection ();
			byte [] multipartData = new byte [] { ( byte ) '\r', ( byte ) '\n', ( byte ) '-', ( byte ) '-' };
			reader.ReadBytes ( 2 );

			while ( true )
			{
				if ( partSeparatorMode )
				{
					byte [] data = reader.ReadBytes ( boundary.Length );
					if ( Encoding.UTF8.GetString ( data ) != boundary )
					{
						tempStream.Write ( multipartData, 0, 4 );
						tempStream.Write ( data, 0, data.Length );
						partSeparatorMode = false;
					}
					else
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
									Path.Combine ( server.TemporaryDirectory,
									Convert.ToBase64String ( Encoding.UTF8.GetBytes ( filename ) ) + Path.GetExtension ( filename )
								), FileMode.Create ) as Stream;
							partSeparatorMode = false;
						}
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
								queue = null;
								break;
							}
						}
						else
						{
							while ( queue.Count != 0 )
									tempStream.WriteByte ( queue.Dequeue () );

							if ( b == multipartData [ 0 ] )
								queue.Enqueue ( b );
							else
								tempStream.WriteByte ( b );

							multipartHeaderIndex = 0;
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

		private void Response ( HttpRequestHeader header, VirtualSite virtualSite )
		{
			// Rewrite url
			foreach ( KeyValuePair<Regex, string> k in virtualSite.RewriteRules )
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
				foreach ( KeyValuePair<string, string> k in virtualSite.SubDirectory )
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
				filename = _Utility.GetFilename ( virtualSite.RootDirectory, header.QueryString, 1 );

			// Cannot found file, apply index filename
			if ( !File.Exists ( filename ) )
			{
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
			foreach ( IPlugin plugin in server.Plugins )
			{
				if ( isPluginProceed = plugin.Run ( args, out responseHeader, out responseStream ) )
					break;
			}
			// If Cannot found Plugin, Processing Original plugin
			if ( !isPluginProceed )
				server.OriginalPlugin.Run ( args, out responseHeader, out responseStream );

			// Send to client
			SendData ( responseHeader, responseStream );
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

			if ( header.Fields.ContainsKey ( HttpHeaderField.Server ) )
				header.Fields [ HttpHeaderField.Server ] = server.ServerName;
			else header.Fields.Add ( HttpHeaderField.Server, server.ServerName );

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