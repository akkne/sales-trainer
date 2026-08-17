using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.23. One nullable column: when the "your deadline is close" notice went out for the
    /// deadline the assignment currently has.
    ///
    /// <para>
    /// No backfill and no concurrent-index script, for the same reason 40.21 and 40.22 shipped
    /// neither: the column is added to a table that is empty in every deployed database — nothing
    /// created an assignment before 40.21, and 40.21 shipped without a screen — so the ACCESS
    /// EXCLUSIVE lock costs nothing and no existing row changes meaning.
    /// </para>
    ///
    /// <para>
    /// No index either, and that is a decision rather than an omission. The deadline sweep's
    /// enumeration is the one query in this service that filters without leading on
    /// <c>OrganizationId</c> — it asks "which organizations have an unannounced deadline coming"
    /// across all of them — so an index for it would have to be a partial index on
    /// <c>(Deadline)</c>, which is the shape the 40.10 convention exists to prevent. It is not worth
    /// the exception: an organization accumulates assignments at the rate a human writes them, so
    /// the scan is over a table measured in hundreds of rows, and the tenant-leading index 40.21
    /// already built serves every per-organization query that follows the enumeration.
    /// </para>
    /// </summary>
    public partial class AddAssignmentDeadlineNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineNoticeSentAt",
                table: "Assignments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeadlineNoticeSentAt",
                table: "Assignments");
        }
    }
}
