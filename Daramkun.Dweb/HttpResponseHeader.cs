using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace Daramkun.Dweb
{
	public struct HttpResponseHeader
	{
		public Version HttpVersion { get; set; }
		public HttpStatusCode Status { get; set; }
		public long ContentLength
		{
			get { return ( long ) Fields [ "Content-Length" ]; }
			set { if ( !Fields.ContainsKey ( "Content-Length" ) ) Fields.Add ( "Content-Length", value ); else Fields [ "Content-Length" ] = value; }
		}
		public ContentType ContentType
		{
			get { return Fields [ "Content-Type" ] as ContentType; }
			set { if ( !Fields.ContainsKey ( "Content-Type" ) ) Fields.Add ( "Content-Type", value ); else Fields [ "Content-Type" ] = value; }
		}

		public Dictionary<string, object> Fields { get; private set; }

		public HttpResponseHeader ( HttpStatusCode status, HttpServer server = null )
			: this ()
		{
			HttpVersion = new Version ( 1, 1 );
			Status = status;
			Fields = new Dictionary<string, object> ();
			Fields.Add ( HttpResponseHeaderField.ContentLength, 0 );
			Fields.Add ( HttpResponseHeaderField.Date, DateTime.UtcNow );
			if ( server != null )
				Fields.Add ( HttpResponseHeaderField.Server, server.ServerName );
		}

		public override string ToString ()
		{
			StringBuilder sb = new StringBuilder ();

			sb.AppendFormat ( "HTTP/{0} ", HttpVersion );
			sb.Append ( GetStatusCode () );

			sb.Append ( "\r\n" );

			foreach ( KeyValuePair<string, object> i in Fields )
			{
				sb.AppendFormat ( "{0}: ", i.Key );
				if ( i.Value is DateTime )
					sb.AppendFormat ( "{0:ddd, d MMM yyyy hh:mm:ss} UTC", ( DateTime ) i.Value );
				else if ( i.Value is int )
					sb.AppendFormat ( "{0}", i.Value );
				else if ( i.Value is string )
					sb.Append ( i.Value );
				else sb.Append ( i.Value.ToString () );
				sb.Append ( "\r\n" );
			}

			sb.Append ( "\r\n" );

			return sb.ToString ();
		}

		private string GetStatusCode ()
		{
			switch ( Status )
			{
				case HttpStatusCode.Continue: return "100 Continue";
				case HttpStatusCode.SwitchingProtocols: return "101 Switching Protocols";

				case HttpStatusCode.OK: return "200 OK";
				case HttpStatusCode.Created: return "201 Created";
				case HttpStatusCode.Accepted: return "202 Accepted";
				case HttpStatusCode.NonAuthoritativeInformation: return "203 Non-Authoritative Information";
				case HttpStatusCode.NoContent: return "204 No Content";
				case HttpStatusCode.ResetContent: return "205 Reset Content";
				case HttpStatusCode.PartialContent: return "206 Partial Content";

				case HttpStatusCode.MultipleChoices: return "300 Multiple Choices";
				case HttpStatusCode.MovedPermanently: return "301 Moved Permanently";
				case HttpStatusCode.Found: return "302 Found";
				case HttpStatusCode.SeeOther: return "303 See Other";
				case HttpStatusCode.NotModified: return "304 Not Modified";
				case HttpStatusCode.UseProxy: return "305 Use Proxy";
				case HttpStatusCode.TemporaryRedirect: return "307 Temporary Redirect";

				case HttpStatusCode.BadRequest: return "400 Bad Request";
				case HttpStatusCode.Unauthorized: return "401 Unauthorized";
				case HttpStatusCode.PaymentRequired: return "402 Payment Required";
				case HttpStatusCode.Forbidden: return "403 Forbidden";
				case HttpStatusCode.NotFound: return "404 Not Found";
				case HttpStatusCode.MethodNotAllowed: return "405 Method Not Allowed";
				case HttpStatusCode.NotAcceptable: return "406 Not Acceptable";
				case HttpStatusCode.ProxyAuthenticationRequired: return "407 Proxy Authentication Required";
				case HttpStatusCode.RequestTimeout: return "408 Request Timeout";
				case HttpStatusCode.Conflict: return "409 Conflict";
				case HttpStatusCode.Gone: return "410 Gone";
				case HttpStatusCode.LengthRequired: return "411 Length Required";
				case HttpStatusCode.PreconditionFailed: return "412 Precondition Failed";
				case HttpStatusCode.RequestEntityTooLarge: return "413 Request Entity Too Large";
				case HttpStatusCode.RequestURITooLong: return "414 Request-URI Too Long";
				case HttpStatusCode.UnsupportedMediaType: return "415 Unsupported Media Type";
				case HttpStatusCode.RequestedRangeNotSatisfiable: return "416 Requested Range Not Satisfiable";
				case HttpStatusCode.ExpectationFailed: return "417 Expectation Failed";

				case HttpStatusCode.InternalServerError: return "500 Internal Server Error";
				case HttpStatusCode.NotImplemented: return "501 Not Implemented";
				case HttpStatusCode.BadGateway: return "502 Bad Gateway";
				case HttpStatusCode.ServiceUnavailable: return "503 Service Unavailable";
				case HttpStatusCode.GatewayTimeout: return "504 Gateway Timeout";
				case HttpStatusCode.HTTPVersionNotSupported: return "505 HTTP Version Not Supported";

				default: throw new ArgumentException ();
			}
		}
	}
}
