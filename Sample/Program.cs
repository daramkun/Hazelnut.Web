// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Net.Mime;
using Hazelnut.Web;
using Hazelnut.Web.Configurations;
using Hazelnut.Web.Handler;

var requestHandler = new RouteRequestHandler();
requestHandler.RegisterRoute(HttpMethod.Get, "/", async (request, response) =>
{
    response.Headers.ContentType = new ContentType("application/json");
    await using var stream = response.OpenTextStream();
    await stream.WriteAsync("{\"result\":\"ok\"}");
});
requestHandler.RegisterRoute(HttpMethod.Get, "/test", async (request, response) =>
{
    response.Headers.ContentType = new ContentType("application/json");
    await using var stream = response.OpenTextStream();
    await stream.WriteAsync("{\"result\":\"test\"}");
});

var serverConfig = new ServerConfiguration(IPAddress.Any, 5678);
var hostConfig = new HostConfiguration(5678, Array.Empty<string>(), requestHandler);

await using var server = new HttpServer(new [] {serverConfig}, new [] {hostConfig});
server.Run();