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
    /// Returns the <c>SET LOCAL</c> statement for the current tenant, or <see langword="null"/>
    /// when there is nothing to set: system-mode connections rely on a <c>BYPASSRLS</c> role
    /// instead of the GUC, and a connection with no organization at all leaves the setting unset
    /// so the fail-closed RLS policy (<c>current_setting(..., true)</c>, missing_ok) filters every
    /// tenant-scoped row rather than erroring.
    /// </summary>
    internal string? BuildSetLocalCommandText()
    {
        if (tenantContext.IsSystem || tenantContext.OrganizationId is not { } organizationId)
        {
            return null;
        }

        return BuildSetLocalCommandText(organizationId);
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
