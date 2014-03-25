using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Daramkun.Dweb
{
	static class _Utility
	{
		public static string ReadToSpace ( BinaryReader reader )
		{
			StringBuilder builder = new StringBuilder ();
			char ch;
			while ( ( ch = reader.ReadChar () ) != ' ' )
				builder.Append ( ch );
			return builder.ToString ();
		}

		public static string ReadToColon ( BinaryReader reader )
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

		public static string ReadToNextLine ( BinaryReader reader )
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

		public static void SkipToNextLine ( BinaryReader reader )
		{
			while ( ( reader.ReadChar () ) != '\r' ) ;
			reader.ReadChar ();
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

			for ( int i = startIndex; i < url.Path.Length; ++i )
				filename.AppendFormat ( "\\{0}", url.Path [ i ] );

			return filename.ToString ();
		}
	}
}

