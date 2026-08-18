using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Tenancy;
using Serilog;

namespace Sellevate.Gamification.DependencyInjection;

/// <summary>
/// The HTTP pipeline, in the one order that is correct.
/// </summary>
public static class GamificationApplicationBuilderExtensions
{
    /// <summary>
    /// Two orderings here are invariants rather than preferences:
    ///
    /// <para>
    /// Phase 40.13. <c>UseSellevateTenantContext</c> populates the scoped <c>ITenantContext</c> from
    /// the gateway-validated <c>X-Organization-Id</c> header, and it runs <b>after</b>
    /// <c>UseAuthorization</c> so the endpoint — and therefore its <c>[TenantScoped]</c> metadata —
    /// is already resolved. Moved earlier, the middleware cannot tell a tenant-scoped route from a
    /// platform one and stops refusing requests that arrive without an organization.
    /// </para>
    ///
    /// <para>
    /// Swagger is registered only outside production, so the schema of the admin surface is not
    /// published to the internet.
    /// </para>
    /// </summary>
    public static WebApplication UseGamificationRequestPipeline(this WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.UseExceptionHandler();
        application.UseSerilogRequestLogging();
        application.UseCors();

        if (application.Environment.IsDevelopment())
        {
            application.UseSwagger();
            application.UseSwaggerUI();
        }

        application.UseAuthentication();
        application.UseAuthorization();

        application.UseSellevateTenantContext();

        application.MapSellevateHealthChecks();

        application.MapControllers();

        return application;
    }
}
