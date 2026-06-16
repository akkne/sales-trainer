using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkillStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Accent = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillStages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillStages_Key",
                table: "SkillStages",
                column: "Key",
                unique: true);

            // Seed the previously frontend-hardcoded funnel stages (key, label, accent, order).
            // "general" is the implicit fallback for unassigned skills and is intentionally
            // not seeded as an editable row.
            migrationBuilder.Sql(@"
                INSERT INTO ""SkillStages"" (""Id"", ""Key"", ""Label"", ""Accent"", ""Order"") VALUES
                    (gen_random_uuid(), 'preparation', 'Подготовка',                'var(--indigo)', 1),
                    (gen_random_uuid(), 'discovery',   'Выявление и оффер',         '#7C3AED',       2),
                    (gen_random_uuid(), 'engagement',  'Контент и коммуникация',    '#0EA5E9',       3),
                    (gen_random_uuid(), 'closing',     'Закрытие сделки',           '#F97316',       4),
                    (gen_random_uuid(), 'retention',   'Удержание клиента',         '#10B981',       5);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillStages");
        }
    }
}
