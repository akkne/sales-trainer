using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Sellevate.Gateway;

/// <summary>
/// Adds the CORS response headers to answers the gateway produces **itself** — a 504 when an
/// upstream times out, a 502 when it is unreachable, a 404 for an unknown route.
///
/// <para>
/// Must be registered ahead of the reverse proxy so it also wraps the proxy's own failure
/// responses. Proxied answers already carry the headers from the downstream service, and a
/// duplicated <c>Access-Control-Allow-Origin</c> is rejected by browsers, so anything that
/// already has one is left untouched. Without this, every gateway-level failure reaches the
/// browser as "blocked by CORS policy" and the real status code — the one that says what
/// actually broke — is invisible to the client.
/// </para>
/// </summary>
internal sealed class GatewayErrorCorsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _allowedOrigins;

    public GatewayErrorCorsMiddleware(RequestDelegate next, IOptions<FrontendOptions> frontendOptions)
    {
        _next = next;
        _allowedOrigins = frontendOptions.Value.Url
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && IsAllowed(origin))
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(HeaderNames.AccessControlAllowOrigin))
                {
                    context.Response.Headers[HeaderNames.AccessControlAllowOrigin] = origin;
                    context.Response.Headers[HeaderNames.AccessControlAllowCredentials] = "true";
                    context.Response.Headers[HeaderNames.AccessControlAllowHeaders] = "*";
                    context.Response.Headers[HeaderNames.AccessControlAllowMethods] = "*";
                    context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.Origin);
                }
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }

    private bool IsAllowed(string origin) =>
        _allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
}
