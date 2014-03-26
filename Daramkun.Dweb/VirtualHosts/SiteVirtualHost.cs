using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Daramkun.Dweb.VirtualHosts
{
	public class SiteVirtualHost : VirtualHost
	{
		public string RootDirectory { get; private set; }
		public Dictionary<string, string> SubDirectory { get; private set; }
		public Dictionary<Regex, string> RewriteRules { get; private set; }

		public SiteVirtualHost ( string serverHost, string rootDirectory )
			: base ( serverHost )
		{
			RootDirectory = rootDirectory;
			SubDirectory = new Dictionary<string, string> ();
			RewriteRules = new Dictionary<Regex, string> ();
		}
	}
}
