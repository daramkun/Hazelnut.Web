using Hazelnut.Web.Handler;

namespace Hazelnut.Web.Authorizes;

public interface IAuthorizeMethod
{
    bool IsAuthorized(Request request, Response response);
}