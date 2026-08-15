using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAuthConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationAuthConfigurations",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderSettings = table.Column<string>(type: "jsonb", nullable: true),
                    AllowedEmailDomains = table.Column<string[]>(type: "text[]", nullable: false),
                    IsJustInTimeProvisioningEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SessionLifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IsMultiFactorAuthenticationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAuthConfigurations", x => x.OrganizationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAuthConfigurations_AllowedEmailDomains",
                table: "OrganizationAuthConfigurations",
                column: "AllowedEmailDomains")
                .Annotation("Npgsql:IndexMethod", "gin");

            // The method is stored as text rather than an int enum so the value in the database
            // reads the same as the value on the wire and in IAuthProvider.Method. The check
            // constraint is what keeps that column a closed set anyway — a stricter guarantee than
            // an int enum, which happily stores 47.
            migrationBuilder.Sql("""
                ALTER TABLE "OrganizationAuthConfigurations"
                    ADD CONSTRAINT "CK_OrganizationAuthConfigurations_Method"
                    CHECK ("Method" IN ('password', 'oidc', 'saml'));
                """);

            // Deliberately no EnableTenantRls here, unlike the Invites table added in 40.7. This
            // row is read on POST /auth/login/start — before authentication, with no tenant
            // context to put into app.organization_id — and the domain lookup is a cross-tenant
            // question by nature ("which organization claims this domain"). Under RLS every login
            // would have to bypass it, which is theatre; see docs/DECISIONS.md (2026-08-15,
            // Phase 40.8) and the remarks on OrganizationAuthConfiguration.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationAuthConfigurations");
        }
    }
}
