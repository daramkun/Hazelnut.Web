using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Daramkun.Dweb
{
	public enum HttpRequestMethod
	{
		Unknown = -1,
		HEAD,
		GET,
		POST,
		PUT,
		DELETE,
		TRACE,
		OPTIONS,
		CONNECT,
	}
}
