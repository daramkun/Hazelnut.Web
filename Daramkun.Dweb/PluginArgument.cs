using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using FieldCollection = System.Collections.Generic.Dictionary<string, string>;

namespace Daramkun.Dweb
{
	public class PluginArgument
	{
		public HttpServer Server { get; set; }

		public HttpRequestMethod RequestMethod { get; set; }
		public HttpUrl Url { get; set; }
		public FieldCollection RequestFields { get; set; }
		public FieldCollection Get { get; set; }
		public FieldCollection Post { get; set; }
		public ContentType ContentType { get; set; }

		public VirtualHost VirtualHost { get; set; }
		public string OriginalFilename { get; set; }
	}
}
