using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Daramkun.Dweb;
using Daramkun.Dweb.Plugins;
using Daramkun.Dweb.Utility;
using Daramkun.Dweb.VirtualHosts;

namespace Dweb.Test
{
	class Program
	{
		static void Main ( string [] args )
		{
			Assembly assembly = Assembly.GetEntryAssembly ();

			HttpServer server = VirtualHostLoader.LoadServerConfiguration ( assembly.GetManifestResourceStream ( "Dweb.Test.80.json" ) );
			//HttpServer sslServer = VirtualHostLoader.LoadServerConfiguration ( assembly.GetManifestResourceStream ( "Dweb.Test.443.json" ) );
			HttpServer proxyServer = VirtualHostLoader.LoadServerConfiguration ( assembly.GetManifestResourceStream ( "Dweb.Test.8080.json" ) );
			server.Plugins.Add ( new TextDeflatePlugin () );
			server.Start ();
			//sslServer.Start ();
			proxyServer.Start ();

			while ( server.IsServerAlive )
			{
				Console.ReadLine ();
				Console.WriteLine ( server.Clients.Count );
				//Console.WriteLine ( sslServer.Clients.Count );
				Console.WriteLine ( proxyServer.Clients.Count );
			}
		}
	}
}
