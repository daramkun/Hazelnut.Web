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
		public Dictionary<Regex, string> RewriteRules { get; private set; }

		public List<string> IndexNames { get; private set; }

		public SiteVirtualHost ( string serverHost, string rootDirectory )
			: base ( serverHost )
		{
			RootDirectory = rootDirectory;
			RewriteRules = new Dictionary<Regex, string> ();
			
			IndexNames = new List<string> ( new string [] { "index.html", "index.htm", "index.dhtml", "index.xhtml" } );
		}
	}
}
