﻿using System;
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
		public string QueryString { get; set; }
		public Version HttpVersion { get; set; }
		public FieldCollection Fields { get; private set; }
		public FieldCollection PostData { get; private set; }

		public HttpRequestHeader ( Stream stream )
			: this ()
		{
			BinaryReader reader = new BinaryReader ( stream );
			
			// First line of Request Header
			RequestMethod = ( HttpRequestMethod ) Enum.Parse ( typeof ( HttpRequestMethod ), ReadToSpace ( reader ).ToUpper () );
			QueryString = ReadToSpace ( reader );
			HttpVersion = new Version ( ReadToNextLine ( reader ).Substring ( 5 ) );
			
			// Read Header Fields
			SkipToNextLine ( reader );
			Fields = new FieldCollection ();

			string key;
			while ( ( key = ReadToColon ( reader ) ) != null )
				Fields.Add ( key, ReadToNextLine ( reader ).Trim () );

			PostData = new FieldCollection ();
		}

		#region Read From NetworkStream
		private string ReadToSpace ( BinaryReader reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			while ( ( ch = reader.ReadChar () ) != ' ' )
				builder.Append ( ch );
			return builder.ToString ();
		}

		private string ReadToColon ( BinaryReader reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			while ( ( ch = reader.ReadChar () ) != ':' )
			{
				if ( ch == '\r' ) { reader.ReadChar (); return null; }
				builder.Append ( ch );
			}
			return builder.ToString ();
		}

		private string ReadToNextLine ( BinaryReader reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			bool isStr = false;
			while ( ( ch = reader.ReadChar () ) == ' ' ) ;
			if ( ch != ' ' ) builder.Append ( ch );
			if ( ch == '"' ) isStr = true;
			while ( ( ch = reader.ReadChar () ) != '\r' || isStr )
			{
				builder.Append ( ch );
				if ( ch == '"' ) isStr = !isStr;
			}
			reader.ReadChar ();
			return builder.ToString ();
		}

		private void SkipToNextLine ( BinaryReader reader )
		{
			while ( ( reader.ReadChar () ) != '\r' ) ;
			reader.ReadChar ();
		}
		#endregion
	}
}
