// See https://aka.ms/new-console-template for more information

using System.Net;
using Hazelnut.Web;
using Hazelnut.Web.Configurations;
using Hazelnut.Web.Handler;

var serverConfig = new ServerConfiguration(IPAddress.Any, 5678);
var hostConfig = new HostConfiguration(5678, Array.Empty<string>(), new FileRequestHandler(@"D:\Temp\www"));

await using var server = new HttpServer(new [] {serverConfig}, new [] {hostConfig});
server.Run();