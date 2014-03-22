using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Daramkun.Dweb
{
	public class VirtualSite
	{
		Dictionary<string, string> subDir;
		Dictionary<Regex, string> rewRule;

		public string ServerHost { get; set; }
		public string Administrator { get; set; }
		public bool IsRedirect { get; set; }
		public string RootDirectory { get; set; }
		public Dictionary<string, string> SubDirectory { get { return subDir; } }
		public Dictionary<Regex, string> RewriteRules { get { return rewRule; } }

		public VirtualSite ( string serverHost, string rootDir, bool isRedirect )
		{
			ServerHost = serverHost;
			IsRedirect = isRedirect;
			RootDirectory = rootDir;
			subDir = new Dictionary<string, string> ();
			rewRule = new Dictionary<Regex, string> ();
		}
	}
}
