using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Daramkun.Dweb
{
	static class _Utility
	{
		public static string ReadToSpace ( Stream reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			while ( ( ch = ( char ) reader.ReadByte () ) != ' ' )
				builder.Append ( ch );
			return builder.ToString ();
		}

		public static string ReadToColon ( Stream reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			while ( ( ch = ( char ) reader.ReadByte () ) != ':' )
			{
				if ( ch == '\r' ) { reader.ReadByte (); return null; }
				builder.Append ( ch );
			}
			return builder.ToString ();
		}

		public static string ReadToNextLine ( Stream reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			bool isStr = false;
			while ( ( ch = ( char ) reader.ReadByte () ) == ' ' ) ;
			if ( ch != ' ' ) builder.Append ( ch );
			if ( ch == '"' ) isStr = true;
			while ( ( ch = ( char ) reader.ReadByte () ) != '\r' || isStr )
			{
				builder.Append ( ch );
				if ( ch == '"' ) isStr = !isStr;
			}
			reader.ReadByte ();
			return builder.ToString ();
		}

		public static void SkipToNextLine ( Stream reader )
		{
			while ( ( reader.ReadByte () ) != '\r' ) ;
			reader.ReadByte ();
		}

		public static string ReadName ( string disposition )
		{
			Regex regex = new Regex ( "name=\"(.*)\"" );
			Match match = regex.Match ( disposition );
			return match.Groups [ 1 ].Value;
		}

		public static string ReadFilename ( string disposition )
		{
			Regex regex = new Regex ( "filename=\"(.*)\"" );
			Match match = regex.Match ( disposition );
			if ( match == null || match.Groups.Count == 1 ) return null;
			return match.Groups [ 1 ].Value;	
		}

		public static string GetFilename ( string baseDirectory, HttpUrl url, int startIndex )
		{
			StringBuilder filename = new StringBuilder ();
			filename.Append ( baseDirectory );
			if ( filename [ filename.Length - 1 ] == '\\' )
				filename.Remove ( filename.Length - 1, 1 );

			for ( int i = startIndex; i < url.Path.Count; ++i )
				filename.AppendFormat ( "\\{0}", url.Path [ i ] );

			return filename.ToString ();
		}
	}
}

