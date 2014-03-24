using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

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
							length += socket.Receive ( temp, contentLength - length >= 1024 ? 1024 : contentLength - length, SocketFlags.None );
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
							ContentType contentType = new ContentType ( header.Fields [ HttpRequestHeaderField.ContentType ] as string );
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
					responseHeader.Fields.Add ( HttpResponseHeaderField.Location, virtualSite.RootDirectory );
					SendData ( responseHeader, null );
				}
				else
				{
					Response ( header, virtualSite );
				}
			}, null );
		}

		private void ReadMultipartPOSTData ( BinaryReader reader, int contentLength, string boundary, Dictionary<string, string> dictionary )
		{
			bool partSeparatorMode = true, firstLooping = true;
			Stream tempStream = null;
			Dictionary<string, string> fields = new Dictionary<string, string> ();
			reader.ReadBytes ( 2 );
			while ( true )
			{
				if ( partSeparatorMode )
				{
					byte [] data = reader.ReadBytes ( boundary.Length );
					if ( Encoding.UTF8.GetString ( data ) != boundary )
					{
						tempStream.Write ( new byte [] { ( byte ) '\r', ( byte ) '\n', ( byte ) '-', ( byte ) '-' }, 0, 4 );
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
							while ( ( key = ReadToColon ( reader ) ) != null )
								fields.Add ( key, ReadToNextLine ( reader ).Trim () );
							string filename = ReadFilename ( fields [ HttpResponseHeaderField.ContentDisposition ] );
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
					byte b = reader.ReadByte ();
					if ( ( char ) b == '\r' )
					{
						byte [] tt = reader.ReadBytes ( 3 );
						if ( Encoding.UTF8.GetString ( tt ) == "\n--" ) partSeparatorMode = true;
						else
						{
							tempStream.WriteByte ( ( byte ) '\r' );
							tempStream.Write ( tt, 0, 3 );
						}
					}
					else tempStream.WriteByte ( b );
				}
				firstLooping = false;
			}
		}

		private void AddToPOST ( Dictionary<string, string> dictionary, Dictionary<string, string> fields, MemoryStream tempStream )
		{
			string filename = ReadFilename ( fields [ HttpResponseHeaderField.ContentDisposition ] );
			dictionary.Add ( ReadName ( fields [ HttpResponseHeaderField.ContentDisposition ] ), filename ?? Encoding.UTF8.GetString ( tempStream.ToArray () ) );
		}

		private string ReadName ( string disposition )
		{
			Regex regex = new Regex ( "name=\"(([a-zA-Z0-9가-힣_]|-| )*)\"" );
			Match match = regex.Match ( disposition );
			return match.Groups [ 1 ].Value;
		}

		private string ReadFilename ( string disposition )
		{
			Regex regex = new Regex ( "filename=\"((.*)*)\"" );
			Match match = regex.Match ( disposition );
			if ( match == null || match.Groups.Count == 1 ) return null;
			return match.Groups [ 1 ].Value;	
		}

		private string ReadToColon ( BinaryReader reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			while ( ( ch = reader.ReadChar () ) != ':' )
			{
				if ( ch == '\r' ) { reader.ReadChar (); return null; }
				builder.Append ( ch );
			}
			return builder.ToString ();
		}

		private string ReadToNextLine ( BinaryReader reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			bool isStr = false;
			while ( ( ch = reader.ReadChar () ) == ' ' ) ;
			if ( ch != ' ' ) builder.Append ( ch );
			if ( ch == '"' ) isStr = true;
			while ( ( ch = reader.ReadChar () ) != '\r' || isStr )
			{
				builder.Append ( ch );
				if ( ch == '"' ) isStr = !isStr;
			}
			reader.ReadChar ();
			return builder.ToString ();
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

		private void Response ( HttpRequestHeader header, VirtualSite virtualSite )
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
			PluginArgument args = new PluginArgument ()
			{
				RequestMethod = header.RequestMethod,
				Url = url,
				Get = url.QueryString,
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

			if ( header.Fields.ContainsKey ( HttpResponseHeaderField.Server ) )
				header.Fields [ HttpResponseHeaderField.Server ] = server.ServerName;
			else header.Fields.Add ( HttpResponseHeaderField.Server, server.ServerName );

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