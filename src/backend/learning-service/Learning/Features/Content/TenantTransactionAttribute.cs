using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Content;

/// <summary>
/// Phase 40.18. Closes the gap <c>TenantTransactionScope</c> named in its own documentation from
/// 40.10 onwards: "the superadmin-only controllers under Features/Admin talk to the content tables
/// directly and open no scope … Phase 40.18 (organization-authored content) has to revisit them."
///
/// <para>
/// It worked while content was global. <c>SET LOCAL app.organization_id</c> is issued when a
/// transaction begins and has no effect outside one, so a controller that never opens one runs
/// every <c>SELECT</c> with the session variable unset — and the content policy
/// (<c>OrganizationId IS NULL OR = current</c>) still returns the global rows, which used to be all
/// of them. The moment an organization owns a row, that same controller stops being able to see it:
/// the administrator opens the technique they just overrode and finds it gone. Fail-closed, and
/// completely invisible in the logs.
/// </para>
///
/// <para>
/// One filter on the controller rather than a scope in each of twenty actions, because the failure
/// mode of the per-action version is somebody adding action twenty-one. The scope is re-entrant, so
/// services that open their own inside the request find this one already open and become no-ops,
/// and the outermost — this one — owns the commit. An action that threw leaves it uncommitted and
/// the scope rolls back on dispose.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class TenantTransactionAttribute() : TypeFilterAttribute(typeof(TenantTransactionFilter));

internal sealed class TenantTransactionFilter(LearningDbContext databaseContext) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var cancellationToken = context.HttpContext.RequestAborted;

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var executedContext = await next();

        if (executedContext.Exception is null)
        {
            await tenantScope.CommitAsync(cancellationToken);
        }
    }
}
