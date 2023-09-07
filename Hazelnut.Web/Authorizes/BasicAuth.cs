using System.Net;
using System.Text;
using Hazelnut.Web.Handler;

namespace Hazelnut.Web.Authorizes;

public class BasicAuth : IAuthorizeMethod
{
    public string Id { get; }
    public string Password { get; }

    public BasicAuth(string id, string password)
    {
        Id = id;
        Password = password;
    }
    
    public bool IsAuthorized(Request request, Response response)
    {
        var authorization = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authorization))
            return false;

        if (!authorization.StartsWith("Basic "))
            return false;

        var idpwBytes = Convert.FromBase64String(authorization["Basic ".Length..]);
        var idpw = Encoding.UTF8.GetString(idpwBytes);

        var separatorIndex = idpw.IndexOf(':');
        if (separatorIndex == -1)
            return false;

        var id = idpw[..separatorIndex];
        var pw = idpw[(separatorIndex + 1)..];

        var authorized = id.Equals(Id) && pw.Equals(Password);

        if (authorized)
        {
            response.StatusCode = HttpStatusCode.OK;
            response.Headers["Authentication-Info"] = "Basic Authentication Success";
        }
        else
        {
            response.StatusCode = HttpStatusCode.Unauthorized;
            response.Headers["WWW-Authenticate"] = "Basic realm=\"Keep\"";
        }

        return authorized;
    }
}