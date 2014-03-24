using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace Daramkun.Dweb
{
	public interface IPlugin
	{
		bool Run ( PluginArgument args, out HttpResponseHeader header, out Stream stream );
	}
}
