using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Daramkun.Dweb
{
	public class HttpUrl
	{
		Dictionary<string, string> queryString = new Dictionary<string, string> ();

		public string [] Path { get; set; }
		public Dictionary<string, string> QueryString { get { return queryString; } }

		public HttpUrl ( string path )
		{
			string [] temp = path.Split ( '#' ) [ 0 ].Split ( '?' );
			Path = temp [ 0 ].Split ( '/' );
			Path [ 0 ] = temp [ 0 ];
			if ( temp.Length > 1 )
			{
				for ( int i = 1; i < Path.Length; ++i )
					Path [ i ] = HttpUtility.UrlDecode ( Path [ i ] );
				temp = temp [ 1 ].Split ( '&' );
				foreach ( string s in temp )
				{
					string [] temp2 = s.Split ( '=' );
					queryString.Add ( temp2 [ 0 ], ( temp2.Length == 2 ) ? temp2 [ 1 ] : null );
				}
			}
		}
	}
}
