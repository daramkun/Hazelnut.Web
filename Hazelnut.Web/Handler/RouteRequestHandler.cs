using System.Diagnostics;
using System.Net;
using System.Reflection;
using Hazelnut.Web.Authorizes;

namespace Hazelnut.Web.Handler;

public class RouteRequestHandler : IRequestHandler
{
    private readonly Dictionary<HttpMethod, List<RouteInfo>> _routes = new();

    public string Location { get; set; } = "/";
    public IAuthorizeMethod? AuthorizeMethod { get; set; } = null;

    public RouteRequestHandler()
    {
        Initialize();
    }

    private void Initialize()
    {
        var methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var method in methods)
        {
            var route = method.GetCustomAttribute<RouteAttribute>();
            if (route == null)
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length != 2)
                continue;

            if (parameters[0].ParameterType != typeof(Request) ||
                parameters[1].ParameterType != typeof(Response))
                continue;

            if (method.ReturnType == typeof(void))
                InternalRegisterRoute(route.Method, route.Route, method.CreateDelegate<SynchronousRouteBody>(this));
            else if (method.ReturnType == typeof(Task))
                InternalRegisterRoute(route.Method, route.Route, method.CreateDelegate<TaskRouteBody>(this));
            else if (method.ReturnType == typeof(ValueTask))
                InternalRegisterRoute(route.Method, route.Route, method.CreateDelegate<ValueTaskRouteBody>(this));
        }
    }

    private void InternalRegisterRoute(RouteMethod method, string route, Delegate body)
    {
        var httpMethod = method switch
        {
            RouteMethod.Get => HttpMethod.Get,
            RouteMethod.Put => HttpMethod.Put,
            RouteMethod.Post => HttpMethod.Post,
            RouteMethod.Delete => HttpMethod.Delete,
            RouteMethod.Head => HttpMethod.Head,
            RouteMethod.Options => HttpMethod.Options,
            RouteMethod.Trace => HttpMethod.Trace,
            RouteMethod.Patch => HttpMethod.Patch,
            RouteMethod.Connect => HttpMethod.Connect,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
        InternalRegisterRoute(httpMethod, route, body);
    }

    private void InternalRegisterRoute(HttpMethod method, string route, Delegate body)
    {
        if (!_routes.TryGetValue(method, out var routes))
        {
            routes = new List<RouteInfo>();
            _routes.Add(method, routes);
        }

        var realBody = body switch
        {
            ValueTaskRouteBody valueTaskRouteBody => valueTaskRouteBody,
            TaskRouteBody taskRouteBody => (request, response) => new ValueTask(taskRouteBody(request, response)),
            SynchronousRouteBody synchronousRouteBody => (request, response) =>
            {
                synchronousRouteBody(request, response);
                return ValueTask.CompletedTask;
            },
            _ => null
        };

        if (realBody == null)
            return;

        routes.Add(new RouteInfo(route, realBody));
    }

    public void RegisterRoute(HttpMethod method, string route, ValueTaskRouteBody body) =>
        InternalRegisterRoute(method, route, body);
    // public void RegisterRoute(HttpMethod method, string route, TaskRouteBody body) =>
    //     InternalRegisterRoute(method, route, body, isCustomAdded: true);
    public void RegisterRoute(HttpMethod method, string route, SynchronousRouteBody body) =>
        InternalRegisterRoute(method, route, body);

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