using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Daramkun.Dweb;

namespace Dweb.Test
{
	class Program
	{
		static void Main ( string [] args )
		{
			HttpServer server = new HttpServer ( new IPEndPoint ( IPAddress.Any, 80 ), 5, null, Console.Out );
			server.AddDefaultMimes ();
			server.VirtualSites.Add ( "*", new VirtualSite ( "*", @"E:\Web", false ) );

			HttpServer sslServer = new HttpServer ( new IPEndPoint ( IPAddress.Any, 443 ), 5, new X509Certificate2 ( @"E:\test.pfx", "eternity" ), Console.Out );
			sslServer.AddDefaultMimes ();
			sslServer.VirtualSites.Add ( "*", new VirtualSite ( "*", @"E:\Web", false ) );


			while ( server.IsServerAlive ) ;
		}
	}
}
