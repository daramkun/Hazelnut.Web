using Hazelnut.Web.Authorizes;

namespace Hazelnut.Web.Handler;

public interface IRequestHandler
{
    string Location { get; }
    IAuthorizeMethod? AuthorizeMethod { get; }
    
    ValueTask OnRequestAsync(Request request, Response response);
}