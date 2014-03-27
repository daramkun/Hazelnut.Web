using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Daramkun.Dweb
{
	public class VirtualHost
	{
		public string ServerHost { get; private set; }
		public string Administrator { get; set; }
		public int MaximumPostSize { get; set; }

		public VirtualHost ( string serverHost )
		{
			ServerHost = serverHost;
			Administrator = "";
			MaximumPostSize = 8388608;	// 8.0MB
		}
	}
}
