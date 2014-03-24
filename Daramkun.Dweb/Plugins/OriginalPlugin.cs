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
		public bool Run ( PluginArgument args, out HttpResponseHeader header, out Stream stream )
		{
			if ( File.Exists ( args.OriginalFilename ) )
			{
				header = new HttpResponseHeader ( HttpStatusCode.OK );
				stream = File.OpenRead ( args.OriginalFilename );
				header.ContentType = args.ContentType;
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
