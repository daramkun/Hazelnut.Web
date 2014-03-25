using System;
using System.IO;

namespace Daramkun.Dweb
{
	public interface IPlugin
	{
		bool Run ( PluginArgument args, out HttpResponseHeader header, out Stream stream );
	}
}
