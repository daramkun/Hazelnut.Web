using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;

namespace Daramkun.Dweb.Plugins
{
	public class OriginalPlugin : IPlugin
	{
		public bool Run ( HttpRequestHeader reqHeader, ContentType mime, string filename, HttpUrl queryString, out HttpResponseHeader header, out Stream stream )
		{
			if ( File.Exists ( filename ) )
			{
				header = new HttpResponseHeader ( HttpStatusCode.OK );
				stream = File.OpenRead ( filename );
				header.ContentType = mime;
				header.ContentLength = stream.Length;
			}
			else
			{
				header = new HttpResponseHeader ( HttpStatusCode.NotFound );
				stream = null;
			}
			return true;
		}
	}
}
