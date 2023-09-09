using System.Net.Mime;
using Hazelnut.Web.Handler;

namespace Sample;

public class CustomRouteRequestHandler : RouteRequestHandler
{
    [Route(RouteMethod.Get, "/")]
    private async ValueTask OnRoot(Request request, Response response)
    {
        response.Headers.ContentType = new ContentType("application/json");
        await using var stream = response.OpenTextStream();
        await stream.WriteAsync("{\"result\":\"ok\"}");
    }

    [Route(RouteMethod.Get, "/test")]
    private async ValueTask OnTest(Request request, Response response)
    {
        response.Headers.ContentType = new ContentType("application/json");
        await using var stream = response.OpenTextStream();
        await stream.WriteAsync("{\"result\":\"test\"}");
    }

    [Route(RouteMethod.Get, "/test2")]
    private async ValueTask OnTest2(Response response)
    {
        response.Headers.ContentType = new ContentType("application/json");
        await using var stream = response.OpenTextStream();
        await stream.WriteAsync("{\"result\":\"test2\"}");
    }

    [Route(RouteMethod.Get, "/test3")]
    private async Task OnTest3(Request request, Response response)
    {
        response.Headers.ContentType = new ContentType("application/json");
        await using var stream = response.OpenTextStream();
        await stream.WriteAsync("{\"result\":\"test3\"}");
    }

    [Route(RouteMethod.Get, "/test4")]
    private void OnTest4(Request request, Response response)
    {
        response.Headers.ContentType = new ContentType("application/json");
        using var stream = response.OpenTextStream();
        stream.Write("{\"result\":\"test4\"}");
    }
}