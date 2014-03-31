using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Daramkun.Dweb.Plugins
{
	public class TextDeflatePlugin : IPlugin
	{
		public bool Run ( PluginArgument args, out HttpResponseHeader header, out Stream stream )
		{
			if ( args.ContentType.MediaType.Substring ( 0, 4 ) == "text" )
			{
				stream = new MemoryStream ();
				using ( DeflateStream ds = new DeflateStream ( stream, CompressionMode.Compress, true ) )
				{
					using ( FileStream fs = new FileStream ( args.OriginalFilename, FileMode.Open ) )
					{
						byte [] buffer = new byte [ 4096 ];
						while ( fs.Position != fs.Length )
						{
							int len = fs.Read ( buffer, 0, 4096 );
							ds.Write ( buffer, 0, len );
						}
					}
				}
				stream.Position = 0;

				header = new HttpResponseHeader ( HttpStatusCode.OK, args.Server );
				header.ContentType = args.ContentType;
				header.HttpVersion = new Version ( 1, 1 );
				header.ContentLength = stream.Length;
				header.Fields.Add ( HttpHeaderField.ContentEncoding, "deflate" );

				return true;
			}
			else { header = new HttpResponseHeader (); stream = null; return false; }
		}
	}
}
