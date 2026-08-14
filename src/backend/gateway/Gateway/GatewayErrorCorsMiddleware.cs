namespace Sellevate.Gateway;

/// <summary>
/// Adds the CORS response headers to answers the gateway produces **itself** — a 504 when an
/// upstream times out, a 502 when it is unreachable, a 404 for an unknown route.
///
/// Proxied answers already carry the headers from the downstream service, and a duplicated
/// <c>Access-Control-Allow-Origin</c> is rejected by browsers, so anything that already has one is
/// left untouched. Without this, every gateway-level failure reaches the browser as
/// "blocked by CORS policy" and the real status code — the one that says what actually broke — is
/// invisible to the client.
/// </summary>
public sealed class GatewayErrorCorsMiddleware
{
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string AllowCredentialsHeader = "Access-Control-Allow-Credentials";
    private const string AllowHeadersHeader = "Access-Control-Allow-Headers";
    private const string AllowMethodsHeader = "Access-Control-Allow-Methods";

    private readonly RequestDelegate _next;
    private readonly string[] _allowedOrigins;

    public GatewayErrorCorsMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _allowedOrigins = (configuration["Frontend:Url"] ?? "http://localhost:3000")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && IsAllowed(origin))
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(AllowOriginHeader))
                {
                    context.Response.Headers[AllowOriginHeader] = origin;
                    context.Response.Headers[AllowCredentialsHeader] = "true";
                    context.Response.Headers[AllowHeadersHeader] = "*";
                    context.Response.Headers[AllowMethodsHeader] = "*";
                    context.Response.Headers.Append("Vary", "Origin");
                }
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }

    private bool IsAllowed(string origin) =>
        _allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
}
