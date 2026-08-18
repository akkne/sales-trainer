using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Sellevate.Gamification.Common.Extensions;

/// <summary>
/// Recognises the one database failure this service treats as success: a unique-constraint
/// violation raised because a concurrent caller inserted the same row first.
///
/// <para>
/// Three write paths — the experience-point ledger, the streak row, and league creation/join — all
/// use the same shape: check, insert, and on a unique violation detach the failed entity and re-read
/// the winner. They previously carried three byte-identical private copies of this predicate, which
/// is exactly the kind of duplication that lets one of them be tightened and the other two not.
/// </para>
///
/// <para>
/// The inner exception's <em>message</em> is matched rather than <c>PostgresException.SqlState</c>,
/// deliberately: the provider type is reached by name so the predicate also answers correctly when
/// the inner exception is a wrapper Npgsql type rather than <c>PostgresException</c> itself, and in
/// that case no strongly-typed <c>SqlState</c> is available to read.
/// </para>
/// </summary>
public static class DatabaseUpdateExceptionExtensions
{
    private const string NpgsqlTypeNameFragment = "Npgsql";
    private const string PostgresExceptionTypeName = "PostgresException";

    /// <summary>
    /// True when <paramref name="exception"/> wraps a PostgreSQL unique-constraint violation
    /// (SQLSTATE 23505). False for every other update failure, which must still propagate.
    /// </summary>
    public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var innerException = exception.InnerException;
        return innerException is not null &&
               (innerException.GetType().Name == PostgresExceptionTypeName ||
                innerException.GetType().FullName?.Contains(NpgsqlTypeNameFragment) == true) &&
               innerException.Message.Contains(PostgresErrorCodes.UniqueViolation);
    }
}
