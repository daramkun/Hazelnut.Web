using System.Net;
using Hazelnut.Web.Authorizes;

namespace Hazelnut.Web.Handler;

public delegate ValueTask RouteBody(Request request, Response response);

public delegate void SynchronousRouteBody(Request request, Response response);

public class RouteRequestHandler : IRequestHandler
{
    private readonly struct RouteInfo
    {
        public readonly string Route;
        public readonly RouteBody Body;

        public readonly string[] Parameters;

        public RouteInfo(string route, RouteBody body)
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

    private readonly Dictionary<HttpMethod, List<RouteInfo>> _routes = new();

    public string Location { get; set; } = "/";
    public IAuthorizeMethod? AuthorizeMethod { get; set; } = null;

    public void RegisterRoute(HttpMethod method, string route, RouteBody body)
    {
        if (!_routes.TryGetValue(method, out var routes))
        {
            routes = new List<RouteInfo>();
            _routes.Add(method, routes);
        }

        routes.Add(new RouteInfo(route, body));
    }

    public void RegisterRoute(HttpMethod method, string route, SynchronousRouteBody body)
    {
        RegisterRoute(method, route, (request, response) =>
        {
            body(request, response);
            return ValueTask.CompletedTask;
        });
    }

    public ValueTask OnRequestAsync(Request request, Response response)
    {
        if (!_routes.TryGetValue(request.Method, out var routes))
        {
            response.StatusCode = HttpStatusCode.NotFound;
            return ValueTask.CompletedTask;
        }

        var queryString = request.QueryString;
        var foundRoute = routes.FirstOrDefault(route =>
            route.Route.Equals(queryString, StringComparison.OrdinalIgnoreCase));
        if (foundRoute is { Route: not null, Body: not null })
            return foundRoute.Body.Invoke(request, response);

        var arguments = new List<string>();
        while (queryString.Contains('/'))
        {
            var slashIndex = queryString.LastIndexOf('/');
            var argument = queryString[(slashIndex + 1)..];
            queryString = queryString[..slashIndex];
            arguments.Insert(0, argument);

            foundRoute = routes.FirstOrDefault(route =>
                route.Route.Equals(queryString, StringComparison.OrdinalIgnoreCase));
            
            if (foundRoute is not { Route: not null, Body: not null })
                continue;
            
            for (var i = 0; i < Math.Min(arguments.Count, foundRoute.Parameters.Length); ++i)
            {
                var parametersDictionary = (Dictionary<string, string>)request.Parameters;
                parametersDictionary[foundRoute.Parameters[i]] = arguments[i];
            }

            return foundRoute.Body.Invoke(request, response);
        }
        
        response.StatusCode = HttpStatusCode.NotFound;
        return ValueTask.CompletedTask;
    }
}