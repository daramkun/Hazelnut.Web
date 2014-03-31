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
			byte [] buffer = new byte [ 1 ];
			while ( reader.Read ( buffer, 0, 1 ) == 1 && ( char ) buffer [ 0 ] != ' ' )
				builder.Append ( ( char ) buffer [ 0 ] );
			return builder.ToString ();
		}

		public static string ReadToColon ( Stream reader )
		{
			StringBuilder builder = new StringBuilder ();
			byte [] buffer = new byte [ 1 ];
			while ( reader.Read ( buffer, 0, 1 ) == 1 && ( char ) buffer [ 0 ] != ':' )
			{
				if ( ( char ) buffer [ 0 ] == '\r' ) { reader.Read ( buffer, 0, 1 ); return null; }
				builder.Append ( ( char ) buffer [ 0 ] );
			}
			return builder.ToString ();
		}

		public static string ReadToNextLine ( Stream reader )
		{
			StringBuilder builder = new StringBuilder ();
			byte [] buffer = new byte [ 1 ];
			bool isStr = false;
			while ( reader.Read ( buffer, 0, 1 ) == 1 && ( char ) buffer [ 0 ] == ' ' ) ;
			if ( ( char ) buffer [ 0 ] != ' ' ) builder.Append ( ( char ) buffer [ 0 ] );
			if ( ( char ) buffer [ 0 ] == '"' ) isStr = true;
			while ( reader.Read ( buffer, 0, 1 ) == 1 && ( ( char ) buffer [ 0 ] != '\r' || isStr ) )
			{
				builder.Append ( ( char ) buffer [ 0 ] );
				if ( ( char ) buffer [ 0 ] == '"' ) isStr = !isStr;
			}
			reader.ReadByte ();
			return builder.ToString ();
		}

		public static void SkipToNextLine ( Stream reader )
		{
			byte [] buffer = new byte [ 1 ];
			do
			{
				reader.Read ( buffer, 0, 1 );
			} while ( ( char ) buffer [ 0 ] != '\r' );
			reader.Read ( buffer, 0, 1 );
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
