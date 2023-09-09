using Hazelnut.Web.Authorizes;

namespace Hazelnut.Web.Handler;

public interface IRequestHandler
{
    string Location { get; set; }
    IAuthorizeMethod? AuthorizeMethod { get; set; }
    
    ValueTask OnRequestAsync(Request request, Response response);
}