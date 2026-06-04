using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations;

/// <remarks>
/// This migration originally shipped without the [Migration] attribute, so
/// EF Core silently skipped it: fresh databases never got the voice columns
/// while long-lived databases had them added out-of-band. The columns are
/// therefore created idempotently so the migration applies cleanly to both.
/// </remarks>
[DbContext(typeof(AppDbContext))]
[Migration("20260406150000_AddVoiceFieldsToDialogMode")]
public partial class AddVoiceFieldsToDialogMode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "DialogModes" ADD COLUMN IF NOT EXISTS "VoiceEnabled" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE "DialogModes" ADD COLUMN IF NOT EXISTS "VoiceId" text NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "DialogModes" DROP COLUMN IF EXISTS "VoiceEnabled";
            ALTER TABLE "DialogModes" DROP COLUMN IF EXISTS "VoiceId";
            """);
    }
}
