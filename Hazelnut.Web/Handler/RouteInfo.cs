namespace Hazelnut.Web.Handler;

internal readonly struct RouteInfo
{
    public readonly string Route;
    public readonly ValueTaskRouteBody Body;

    public readonly string[] Parameters;

    public RouteInfo(string route, ValueTaskRouteBody body)
    {
        Route = route;
        Body = body;

        Parameters = !route.Contains("/:")
            ? Array.Empty<string>()
            : route.Split('/')
                .Reverse()
                .TakeWhile(parameter => parameter[0] == ':')
                .Select(parameter => parameter[1..])
                .Reverse()
                .ToArray();

        if (Parameters.Length <= 0)
            return;
            
        var slashIndex = Route.Length - 1;
        for (var i = 0; i < Parameters.Length; ++i)
            slashIndex = route.LastIndexOf('/', slashIndex + 1);
        Route = route[slashIndex..];
    }
}