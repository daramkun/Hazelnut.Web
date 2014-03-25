using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using FieldCollection = System.Collections.Generic.Dictionary<string, string>;

namespace Daramkun.Dweb
{
	public struct PluginArgument
	{
		public HttpRequestMethod RequestMethod { get; set; }
		public HttpUrl Url { get; set; }
		public FieldCollection RequestFields { get; set; }
		public FieldCollection Get { get; set; }
		public FieldCollection Post { get; set; }
		public ContentType ContentType { get; set; }

		public string OriginalFilename { get; set; }
	}
}
