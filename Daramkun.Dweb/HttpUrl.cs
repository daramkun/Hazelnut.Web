using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Daramkun.Dweb
{
	public class HttpUrl
	{
		Dictionary<string, string> queryString = new Dictionary<string, string> ();

		public string Path { get; set; }
		public Dictionary<string, string> QueryString { get { return queryString; } }

		public HttpUrl ( string path )
		{
			string [] temp = path.Split ( '#' ) [ 0 ].Split ( '?' );
			Path = temp [ 0 ];
			temp = temp [ 1 ].Split ( '&' );
			foreach ( string s in temp )
			{
				string [] temp2 = s.Split ( '=' );
				queryString.Add ( temp2 [ 0 ], ( temp2.Length == 2 ) ? temp2 [ 1 ] : null );
			}
		}
	}
}
