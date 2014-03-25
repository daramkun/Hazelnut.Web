using System;
using System.Collections.Generic;
using System.Diagnostics;
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
	public class HttpAccept : IDisposable
	{
		public HttpServer Server { get; private set; }
		public Socket Socket { get; private set; }

		public HttpAccept ( HttpServer server, Socket socket )
		{
			Server = server;
			Socket = socket;

			ReceiveRequest ();
		}

		~HttpAccept() { Dispose ( false ); }

		protected virtual void Dispose ( bool isDisposing )
		{
			if ( isDisposing )
			{
				Socket.Disconnect ( false );
				Socket.Dispose ();
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
				HttpRequestHeader header = new HttpRequestHeader ();

				using ( Stream networkStream = new NetworkStream ( Socket, false ) )
				{
					try
					{
						header = new HttpRequestHeader ( networkStream );
						Server.WriteLog ( "V{0}, [{1}][{2}] {3}", header.HttpVersion, header.RequestMethod, header.Fields [ HttpHeaderField.Host ], header.QueryString );

						// Response start
						VirtualSite virtualSite = null;

						if ( header.Fields.ContainsKey ( HttpHeaderField.Host ) )
							if ( Server.VirtualSites.ContainsKey ( header.Fields [ HttpHeaderField.Host ] as string ) )
								virtualSite = Server.VirtualSites [ header.Fields [ HttpHeaderField.Host ] as string ];
						if ( virtualSite == null )
							virtualSite = Server.VirtualSites.First ().Value;

						if ( header.Fields.ContainsKey ( HttpHeaderField.ContentLength ) )
						{
							int contentLength = int.Parse ( header.Fields [ HttpHeaderField.ContentLength ] as string );
							if ( contentLength > virtualSite.MaximumPostSize )
							{
								byte [] temp = new byte [ 1024 ];
								int length = 0;
								while ( length == contentLength )
									length += Socket.Receive ( temp, contentLength - length >= 1024 ? 1024 : contentLength - length, SocketFlags.None );
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
										int len = Socket.Receive ( temp, 1024, SocketFlags.None );
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
					}
					catch { Server.SocketIsDead ( this ); return; }
				}
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
			Socket.Send ( headerData );
			if ( stream != null )
			{
				byte [] data = new byte [ 1024 ];
				while ( stream.Position != stream.Length )
				{
					Socket.Send ( data, stream.Read ( data, 0, data.Length ), SocketFlags.None );
				}
				stream.Dispose ();
			}
		}
	}
}