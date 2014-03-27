using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Daramkun.Dweb;
using Daramkun.Dweb.VirtualHosts;

namespace Dweb.Test
{
	class Program
	{
		static void Main ( string [] args )
		{
			HttpServer server = new HttpServer ( new IPEndPoint ( IPAddress.Any, 80 ), 5, null, Console.Out );
			server.AddDefaultMimes ();
			server.VirtualSites.Add ( "*", new SiteVirtualHost ( "*", @"E:\Web" ) );

			HttpServer sslServer = new HttpServer ( new IPEndPoint ( IPAddress.Any, 443 ), 5, new X509Certificate2 ( @"E:\test.pfx", "eternity" ), Console.Out );
			sslServer.AddDefaultMimes ();
			sslServer.VirtualSites.Add ( "*", new SiteVirtualHost ( "*", @"E:\Web" ) );

			HttpServer proxyServer = new HttpServer ( new IPEndPoint ( IPAddress.Any, 8080 ), 5, null, Console.Out );
			proxyServer.VirtualSites.Add ( "*", new ProxyVirtualHost ( "*", @"http://www.grow.or.kr" ) );

			while ( server.IsServerAlive )
			{
				Console.ReadLine ();
				Console.WriteLine ( server.Clients.Count );
				Console.WriteLine ( sslServer.Clients.Count );
				Console.WriteLine ( proxyServer.Clients.Count );
			}
		}
	}
}
