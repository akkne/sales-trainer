using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.Gateway;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    var lokiUrl = context.Configuration["Logging:Loki:Url"] ?? "http://loki:3100";

    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.GrafanaLoki(
            lokiUrl,
            labels:
            [
                new LokiLabel { Key = "service", Value = "sellevate-gateway" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Gateway");
});

// ── Central JWT validation ───────────────────────────────────────────────────
// The gateway validates the token once (same signing key/issuer/audience as the
// Identity issuer). Tokens are NOT required here — public monolith endpoints
// (login, swagger, metrics) must still pass through. When a valid token IS present
// we forward trusted X-User-* headers downstream; otherwise the downstream service
// enforces its own [Authorize] as it does today (strangler passthrough).
var jwtSigningKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwtOptions =>
    {
        jwtOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSellevateHealthChecks();

// ── YARP reverse proxy ───────────────────────────────────────────────────────
// Routes/clusters come from the "ReverseProxy" config section. The monolith is
// retired (Phase 9): every prefix is owned by a microservice cluster and there is
// no catch-all, so an unknown route returns 404 instead of falling through.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestTransform(requestContext =>
        {
            IdentityForwarding.Apply(requestContext.ProxyRequest.Headers, requestContext.HttpContext.User);
            return ValueTask.CompletedTask;
        });
    });

var application = builder.Build();

application.UseSerilogRequestLogging();

application.MapSellevateHealthChecks();

application.UseAuthentication();
application.UseAuthorization();

application.MapReverseProxy();

application.Run();

// Exposed so the integration test host (WebApplicationFactory) can boot the gateway.
public partial class Program { }
