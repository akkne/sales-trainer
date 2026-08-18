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

builder.Services.Configure<FrontendOptions>(
    builder.Configuration.GetSection(FrontendOptions.SectionName));

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

application.UseMiddleware<GatewayErrorCorsMiddleware>();

application.MapSellevateHealthChecks();

application.UseAuthentication();
application.UseAuthorization();

application.MapReverseProxy();

application.Run();

/// <summary>
/// Composition root of the API gateway. Public and <c>partial</c> only so the integration test
/// host (<c>WebApplicationFactory&lt;Program&gt;</c>) can boot the real pipeline.
///
/// <para>
/// A JWT is validated centrally here — same signing key, issuer and audience as the Identity
/// issuer — but is never <em>required</em>: public endpoints (login, swagger, metrics) must still
/// pass through anonymously. When a valid token is present the validated identity is forwarded
/// downstream as trusted headers (see <see cref="IdentityForwarding"/>); otherwise the downstream
/// service enforces its own <c>[Authorize]</c>.
/// </para>
///
/// <para>
/// Routes and clusters come exclusively from the <c>ReverseProxy</c> configuration section. The
/// monolith is retired (Phase 9): every prefix is owned by a microservice cluster and there is
/// deliberately no catch-all route, so an unknown path returns 404 instead of falling through.
/// </para>
/// </summary>
public partial class Program { }
