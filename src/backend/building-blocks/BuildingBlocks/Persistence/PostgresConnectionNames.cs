namespace Sellevate.BuildingBlocks.Persistence;

public static class PostgresConnectionNames
{
    /// <summary>
    /// The connection every request-time query and write uses. This is the one that moves to the
    /// <c>sellevate_app</c> role (NOSUPERUSER, NOBYPASSRLS, owns nothing) when row-level security is
    /// switched on, which is the entire point of separating it from <see cref="Migrations"/>.
    /// </summary>
    public const string Runtime = "Postgres";

    /// <summary>
    /// The connection that creates the database and applies EF migrations. It keeps the owning role,
    /// because a role subject to its own tables' RLS cannot run DDL — and, worse, would run it
    /// "successfully" against zero rows.
    ///
    /// <para>
    /// Optional: when it is not configured, <see cref="PostgresConnectionStrings.Migrations"/> falls
    /// back to <see cref="Runtime"/>, which is exactly today's behaviour. Nothing changes for an
    /// installation until the operator sets this key.
    /// </para>
    /// </summary>
    public const string Migrations = "PostgresMigrations";
}
