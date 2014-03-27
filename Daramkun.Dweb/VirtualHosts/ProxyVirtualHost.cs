using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Daramkun.Dweb.VirtualHosts
{
	public class ProxyVirtualHost : VirtualHost
	{
		public string ProxyAddress { get; private set; }

		public ProxyVirtualHost ( string serverHost, string proxyAddress )
			: base ( serverHost )
		{
			ProxyAddress = proxyAddress;
		}
	}
}
