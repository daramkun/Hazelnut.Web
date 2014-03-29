using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Daramkun.Blockar.Json;
using Daramkun.Dweb.VirtualHosts;

namespace Daramkun.Dweb.Utility
{
	public static class VirtualHostLoader
	{
		public static VirtualHost CreateVirtualHost ( JsonContainer document )
		{
			VirtualHost virtualHost = null;

			switch ( document [ "type" ] as string )
			{
				case "site":
					virtualHost = new SiteVirtualHost ( document [ "host" ] as string, document [ "root" ] as string );
					
					if ( document.Contains ( "subdir" ) )
					{
						foreach ( KeyValuePair<object, object> k in ( document [ "subdir" ] as JsonContainer ).GetDictionaryEnumerable () )
							( virtualHost as SiteVirtualHost ).SubDirectory.Add ( k.Key as string, k.Value as string );
					}
					
					if ( document.Contains ( "rewrite" ) )
					{
						foreach ( KeyValuePair<object, object> k in ( document [ "rewrite" ] as JsonContainer ).GetDictionaryEnumerable () )
							( virtualHost as SiteVirtualHost ).RewriteRules.Add ( new Regex ( k.Key as string ), k.Value as string );
					}
					break;

				case "redirect":
					virtualHost = new RedirectVirtualHost ( document [ "host" ] as string, document [ "redirect" ] as string );
					break;

				case "proxy":
					virtualHost = new ProxyVirtualHost ( document [ "host" ] as string, document [ "proxy" ] as string );
					break;
			}

			return virtualHost;
		}

		public static HttpServer LoadServerConfiguration ( Stream stream )
		{
			JsonContainer document = new JsonContainer ( stream );
			IPEndPoint endPoint = new IPEndPoint (
				document.Contains ( "address" ) ?
					IPAddress.Parse ( document [ "address" ] as string ) :
					IPAddress.Any,
				( int ) document [ "port" ]
			);

			X509Certificate2 cert = null;
			if ( document.Contains ( "cert" ) )
			{
				string certString = document [ "cert" ] as string;
				if ( certString.Substring ( 0, 7 ) == "base64:" )
					cert = new X509Certificate2 ( Convert.FromBase64String ( certString.Substring ( 7, certString.Length - 7 ) ), document [ "certpw" ] as string );
				else
					cert = new X509Certificate2 ( certString, document [ "certpw" ] as string );
			}

			TextWriter logStream = null;
			if ( document.Contains ( "log" ) )
			{
				switch ( document [ "log" ] as string )
				{
					case "stdout": logStream = Console.Out; break;
					case "stderr": logStream = Console.Error; break;
					default: logStream = new StreamWriter ( new FileStream ( document [ "log" ] as string, FileMode.Append ) ); break;
				}
			}

			HttpServer server = new HttpServer ( endPoint, document.Contains ( "backlog" ) ? ( int ) document [ "backlog" ] : 5, cert, logStream );

			if ( document.Contains ( "virtualhost" ) )
			{
				foreach ( JsonContainer v in ( document [ "virtualhost" ] as JsonContainer ).GetListEnumerable () )
				{
					VirtualHost vh = CreateVirtualHost ( v );
					server.VirtualHosts.Add ( vh.ServerHost, vh );
				}
			}

			if ( document.Contains ( "mime" ) )
			{
				if ( document [ "mime" ] is string )
				{
					if ( document [ "mime" ] as string == "default" )
						server.AddDefaultMimes ();
					else throw new ArgumentException ();
				}
				else if ( document [ "mime" ] is JsonContainer )
				{
					foreach ( KeyValuePair<object, object> k in ( document [ "mime" ] as JsonContainer ).GetDictionaryEnumerable () )
						server.Mimes.Add ( k.Key as string, new ContentType ( k.Value as string ) );
				}
				else throw new ArgumentException ();
			}

			return server;
		}
	}
}
