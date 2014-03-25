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
		public string ServerHost { get; set; }
		public string Administrator { get; set; }
		public bool IsRedirect { get; set; }
		public string RootDirectory { get; set; }
		public int MaximumPostSize { get; set; }
		public Dictionary<string, string> SubDirectory { get; private set; }
		public Dictionary<Regex, string> RewriteRules { get; private set; }

		public VirtualSite ( string serverHost, string rootDir, bool isRedirect )
		{
			ServerHost = serverHost;
			IsRedirect = isRedirect;
			RootDirectory = rootDir;
			MaximumPostSize = 8388608;								// 8.0MB
			SubDirectory = new Dictionary<string, string> ();
			RewriteRules = new Dictionary<Regex, string> ();
		}
	}
}
