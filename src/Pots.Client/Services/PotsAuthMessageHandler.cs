using System.Net.Http.Headers;

namespace Pots.Client.Services;

public sealed class PotsAuthMessageHandler : DelegatingHandler
{
    private readonly AuthClient _auth;

    public PotsAuthMessageHandler(AuthClient auth)
    {
        _auth = auth;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_auth.CachedJwt))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.CachedJwt);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
