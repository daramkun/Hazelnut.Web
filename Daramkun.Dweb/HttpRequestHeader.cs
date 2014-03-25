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
			BinaryReader reader = new BinaryReader ( stream );
			
			// First line of Request Header
			RequestMethod = ( HttpRequestMethod ) Enum.Parse ( typeof ( HttpRequestMethod ),
				_Utility.ReadToSpace ( reader ).ToUpper () );
			QueryString = new HttpUrl ( _Utility.ReadToSpace ( reader ) );
			HttpVersion = new Version ( _Utility.ReadToNextLine ( reader ).Substring ( 5 ) );
			
			// Read Header Fields
			_Utility.SkipToNextLine ( reader );
			Fields = new FieldCollection ();

			string key;
			while ( ( key = _Utility.ReadToColon ( reader ) ) != null )
				Fields.Add ( key, _Utility.ReadToNextLine ( reader ).Trim () );

			PostData = new FieldCollection ();
		}
	}
}
