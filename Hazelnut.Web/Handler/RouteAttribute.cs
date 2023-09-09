namespace Hazelnut.Web.Handler;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RouteAttribute : Attribute
{
    public RouteMethod Method { get; }
    public string Route { get; }

    public RouteAttribute(RouteMethod method, string route)
    {
        Method = method;
        Route = route;
    }
}