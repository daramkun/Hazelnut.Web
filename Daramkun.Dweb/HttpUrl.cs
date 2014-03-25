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
		public string [] Path { get; set; }
		public FieldCollection QueryString { get; private set; }

		public HttpUrl ( string path )
		{
			string [] temp = path.Split ( '#' ) [ 0 ].Split ( '?' );
			Path = temp [ 0 ].Split ( '/' );
			Path [ 0 ] = temp [ 0 ];
			QueryString = new FieldCollection ();
			if ( temp.Length > 1 )
			{
				temp = temp [ 1 ].Split ( '&' );
				foreach ( string s in temp )
				{
					string [] temp2 = s.Split ( '=' );
					QueryString.Add ( temp2 [ 0 ], ( temp2.Length == 2 ) ? HttpUtility.UrlDecode ( temp2 [ 1 ] ) : null );
				}
			}
		}

		public override string ToString ()
		{
			StringBuilder builder = new StringBuilder ();
			builder.Append ( Path [ 0 ] );
			builder.Append ( '?' );
			foreach ( KeyValuePair<string, string> pair in QueryString )
				builder.AppendFormat ( "{0}={1}&", pair.Key, HttpUtility.UrlEncode ( pair.Value ) );
			builder.Remove ( builder.Length - 1, 1 );
			return builder.ToString ();
		}
	}
}
