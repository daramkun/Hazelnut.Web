using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FieldCollection = System.Collections.Generic.Dictionary<string, string>;

namespace Daramkun.Dweb
{
    public struct HttpRequestHeader
    {
		public HttpRequestMethod RequestMethod { get; set; }
		public HttpUrl QueryString { get; set; }
		public Version HttpVersion { get; set; }
		public FieldCollection Fields { get; private set; }
		public FieldCollection PostData { get; private set; }

		public HttpRequestHeader ( Stream stream )
			: this ()
		{
			// First line of Request Header
			RequestMethod = ( HttpRequestMethod ) Enum.Parse ( typeof ( HttpRequestMethod ),
				_Utility.ReadToSpace ( stream ).ToUpper () );
			QueryString = new HttpUrl ( _Utility.ReadToSpace ( stream ) );
			HttpVersion = new Version ( _Utility.ReadToNextLine ( stream ).Substring ( 5 ) );
			
			// Read Header Fields
			_Utility.SkipToNextLine ( stream );
			Fields = new FieldCollection ();

			string key;
			while ( ( key = _Utility.ReadToColon ( stream ) ) != null )
				if ( !Fields.ContainsKey ( key ) )
					Fields.Add ( key, _Utility.ReadToNextLine ( stream ).Trim () );

			PostData = new FieldCollection ();
		}

		public override string ToString ()
		{
			StringBuilder builder = new StringBuilder ();
			builder.Append ( RequestMethod.ToString () );
			builder.Append ( " " );
			builder.Append ( QueryString.ToString () );
			builder.Append ( " " );
			builder.AppendFormat ( "HTTP/{0}", HttpVersion );
			builder.Append ( "\r\n" );
			foreach ( KeyValuePair<string, string> s in Fields )
				builder.AppendFormat ( "{0}: {1}\r\n", s.Key, s.Value );
			builder.Append ( "\r\n" );
			return builder.ToString ();
		}
	}
}
