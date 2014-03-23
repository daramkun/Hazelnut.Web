using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Daramkun.Dweb;

namespace Dweb.Test
{
	class Program
	{
		static void Main ( string [] args )
		{
			HttpServer server = new HttpServer ( new IPEndPoint ( IPAddress.Any, 80 ), 5, Console.Out );
			server.AddDefaultMimes ();
			server.VirtualSites.Add ( "*", new VirtualSite ( "*", @"E:\Web", false ) );
			while ( server.IsServerAlive ) ;
		}
	}
}
