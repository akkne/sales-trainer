using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sellevate.Ai.Infrastructure.Data;

/// <summary>
/// Phase 40.18. Wraps a controller's actions in one <see cref="AiTenantTransactionScope"/>, so that
/// <c>SET LOCAL app.organization_id</c> is actually in force while they read. One filter on the
/// controller rather than a scope in each action, because the failure mode of the per-action version
/// is somebody adding the next action.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class TenantTransactionAttribute() : TypeFilterAttribute(typeof(TenantTransactionFilter));

internal sealed class TenantTransactionFilter(AiDbContext databaseContext) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var cancellationToken = context.HttpContext.RequestAborted;

        await using var tenantScope = await AiTenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var executedContext = await next();

        if (executedContext.Exception is null)
        {
            await tenantScope.CommitAsync(cancellationToken);
        }
    }
}
