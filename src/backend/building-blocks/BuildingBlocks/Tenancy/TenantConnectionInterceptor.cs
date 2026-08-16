using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// Sets the Postgres session variable that tenant row-level-security policies read
/// (<see cref="OrganizationIdSettingName"/>), using <c>SET LOCAL</c> — never a bare <c>SET</c> —
/// so the value can never survive past the current transaction and leak onto the next request
/// that borrows the same pooled connection. See docs/TENANCY/TENANCY.md §1.5 and
/// docs/DECISIONS.md ("SET LOCAL vs SET", 2026-08-15) for why this shape was chosen.
///
/// <para>
/// <c>SET LOCAL</c> only has an effect inside an active transaction. EF Core already wraps every
/// <c>SaveChangesAsync</c> call in an implicit transaction, so hooking
/// <see cref="TransactionStarted"/> / <see cref="TransactionStartedAsync"/> alone covers every
/// write automatically. Tenant-scoped READ paths have no implicit transaction and must open one
/// explicitly (<c>await using var transaction = await context.Database.BeginTransactionAsync();</c>)
/// to get the same protection — this interceptor cannot retrofit a transaction around a bare
/// <c>SELECT</c>. That requirement is the responsibility of the service that rolls tenant-scoped
/// reads out (Stage C, 40.10+), not of this building block.
/// </para>
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenantContext) : IDbTransactionInterceptor
{
    public const string OrganizationIdSettingName = "app.organization_id";

    public DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        var commandText = BuildSetLocalCommandText();
        if (commandText is not null)
        {
            using var command = CreateSetLocalCommand(result, commandText);
            command.ExecuteNonQuery();
        }

        return result;
    }

    public async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        var commandText = BuildSetLocalCommandText();
        if (commandText is not null)
        {
            await using var command = CreateSetLocalCommand(result, commandText);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// The Postgres session variable that tells a tenant policy's <c>USING</c> clause the caller is
    /// validated platform staff and may read every organization's rows. Set only from
    /// <see cref="ITenantContext.IsPlatformWide"/>, which in a request comes only from the `role`
    /// claim of a validated token (see <see cref="TenantContextMiddleware"/>) — never from a
    /// header, body, query or route value a client could write.
    ///
    /// <para>
    /// It widens reads alone. The <c>WITH CHECK</c> half of every policy ignores it, so a platform
    /// administrator still cannot write a row into an organization they did not name explicitly.
    /// </para>
    /// </summary>
    public const string PlatformModeSettingName = "app.platform_mode";

    /// <summary>
    /// Returns the <c>SET LOCAL</c> statements for the current tenant context, or
    /// <see langword="null"/> when there is nothing to set: system-mode connections rely on a
    /// <c>BYPASSRLS</c> role instead of the GUCs, and a connection with neither an organization nor
    /// platform-wide mode leaves both settings unset so the fail-closed RLS policy
    /// (<c>current_setting(..., true)</c>, missing_ok) filters every tenant-scoped row rather than
    /// erroring.
    ///
    /// <para>
    /// Platform-wide mode and an organization can both be present — a Sellevate administrator who
    /// also belongs to an organization reads across all of them and writes into theirs — so the two
    /// statements are emitted independently rather than as an either/or.
    /// </para>
    /// </summary>
    internal string? BuildSetLocalCommandText()
    {
        if (tenantContext.IsSystem)
        {
            return null;
        }

        var statements = new List<string>(capacity: 2);

        if (tenantContext.OrganizationId is { } organizationId)
        {
            statements.Add(BuildSetLocalCommandText(organizationId));
        }

        if (tenantContext.IsPlatformWide)
        {
            statements.Add(BuildPlatformModeSetLocalCommandText());
        }

        return statements.Count == 0 ? null : string.Join("; ", statements);
    }

    /// <summary>
    /// Pure formatting, exposed so the exact SQL text is unit-testable without a real ADO.NET
    /// transaction. <c>SET LOCAL</c> does not accept bind parameters — it is a configuration
    /// statement, not DML — so the value is interpolated directly. That is not an injection
    /// surface: <paramref name="organizationId"/> is a <see cref="Guid"/> formatted with the
    /// fixed <c>"D"</c> pattern (hyphenated hex only), never free-form user input.
    /// </summary>
    internal static string BuildSetLocalCommandText(Guid organizationId)
        => $"SET LOCAL {OrganizationIdSettingName} = '{organizationId:D}'";

    /// <summary>
    /// The platform-mode counterpart. A constant string with no interpolation at all — the mode is
    /// on or the statement is not emitted.
    /// </summary>
    internal static string BuildPlatformModeSetLocalCommandText()
        => $"SET LOCAL {PlatformModeSettingName} = 'on'";

    private static DbCommand CreateSetLocalCommand(DbTransaction transaction, string commandText)
    {
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Transaction has no associated connection.");

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        return command;
    }
}
