using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;

namespace Daramkun.Dweb
{
	public struct PluginArgument
	{
		public HttpRequestMethod RequestMethod { get; set; }
		public HttpUrl Url { get; set; }
		public Dictionary<string, object> RequestFields { get; set; }
		public Dictionary<string, string> Get { get; set; }
		public Dictionary<string, string> Post { get; set; }
		public ContentType ContentType { get; set; }

		public string OriginalFilename { get; set; }
	}
}
