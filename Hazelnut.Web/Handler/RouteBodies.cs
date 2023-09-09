namespace Hazelnut.Web.Handler;

public delegate ValueTask ValueTaskRouteBody(Request request, Response response);
public delegate void SynchronousRouteBody(Request request, Response response);
public delegate Task TaskRouteBody(Request request, Response response);