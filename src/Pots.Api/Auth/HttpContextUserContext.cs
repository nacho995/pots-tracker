using System.Security.Claims;
using Pots.Infrastructure.RowLevelSecurity;

namespace Pots.Api.Auth;

// Request-scoped IUserContext that reads the authenticated user id from the
// "sub" claim (set by the JWT bearer handler). When no JWT is present (anonymous
// endpoints, request before auth middleware runs), CurrentUserId returns null
// and the RLS interceptor pins '' → no rows visible.
public sealed class HttpContextUserContext : IUserContext
{
    private readonly IHttpContextAccessor _http;

    public HttpContextUserContext(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Guid? CurrentUserId
    {
        get
        {
            var sub = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _http.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
