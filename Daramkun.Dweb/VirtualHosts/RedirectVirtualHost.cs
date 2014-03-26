using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Daramkun.Dweb.VirtualHosts
{
	public class RedirectVirtualHost : VirtualHost
	{
		public string Redirect { get; private set; }
		
		public RedirectVirtualHost ( string serverHost, string redirect )
			: base ( serverHost )
		{
			Redirect = redirect;
		}
	}
}
