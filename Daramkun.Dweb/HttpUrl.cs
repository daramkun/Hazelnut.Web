using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using FieldCollection = System.Collections.Generic.Dictionary<string, string>;

namespace Daramkun.Dweb
{
	public class HttpUrl
	{
		FieldCollection queryString = new FieldCollection ();

		public string [] Path { get; set; }
		public FieldCollection QueryString { get { return queryString; } }

		public HttpUrl ( string path )
		{
			string [] temp = path.Split ( '#' ) [ 0 ].Split ( '?' );
			Path = temp [ 0 ].Split ( '/' );
			Path [ 0 ] = temp [ 0 ];
			if ( temp.Length > 1 )
			{
				temp = temp [ 1 ].Split ( '&' );
				foreach ( string s in temp )
				{
					string [] temp2 = s.Split ( '=' );
					queryString.Add ( temp2 [ 0 ], ( temp2.Length == 2 ) ? HttpUtility.UrlDecode ( temp2 [ 1 ] ) : null );
				}
			}
		}
	}
}
