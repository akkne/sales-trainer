using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTechniques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechniqueCategories",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Color = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueCategories", x => x.Slug);
                });

            migrationBuilder.CreateTable(
                name: "Techniques",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CategorySlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    PrimarySkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Techniques", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTechniqueProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    MasteryPercent = table.Column<int>(type: "integer", nullable: false),
                    PracticeCount = table.Column<int>(type: "integer", nullable: false),
                    LastPracticedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTechniqueProgress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    MetricsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechniqueCases_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueCoaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvatarSeed = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Quote = table.Column<string>(type: "text", nullable: false),
                    ChallengesJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueCoaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechniqueCoaches_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueDialogTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    AnnotationsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueDialogTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechniqueDialogTurns_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueSkills",
                columns: table => new
                {
                    TechniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueSkills", x => new { x.TechniqueId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_TechniqueSkills_Techniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "Techniques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueCases_TechniqueId_OrderIndex",
                table: "TechniqueCases",
                columns: new[] { "TechniqueId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueCoaches_TechniqueId",
                table: "TechniqueCoaches",
                column: "TechniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueDialogTurns_TechniqueId_OrderIndex",
                table: "TechniqueDialogTurns",
                columns: new[] { "TechniqueId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_CategorySlug",
                table: "Techniques",
                column: "CategorySlug");

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_Slug",
                table: "Techniques",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_SortOrder",
                table: "Techniques",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueSkills_SkillId",
                table: "TechniqueSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechniqueProgress_UserId",
                table: "UserTechniqueProgress",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechniqueProgress_UserId_TechniqueId",
                table: "UserTechniqueProgress",
                columns: new[] { "UserId", "TechniqueId" },
                unique: true);

            migrationBuilder.InsertData(
                table: "TechniqueCategories",
                columns: new[] { "Slug", "Label", "Color", "SortOrder" },
                values: new object[,]
                {
                    { "objections", "Возражения", "var(--rust)", 1 },
                    { "cold-calls", "Холодные звонки", "var(--indigo)", 2 },
                    { "closing", "Закрытие", "var(--olive)", 3 },
                    { "discovery", "Квалификация", "var(--clay)", 4 },
                    { "rapport", "Rapport", "var(--sage)", 5 },
                    { "negotiation", "Переговоры", "var(--ink-2)", 6 }
                });

            migrationBuilder.Sql(@"
                INSERT INTO ""Techniques"" (
                    ""Id"", ""Slug"", ""Name"", ""Summary"", ""Body"",
                    ""CategorySlug"", ""Tags"", ""PrimarySkillId"", ""SortOrder"",
                    ""CreatedAt"", ""UpdatedAt""
                )
                SELECT
                    r.""Id"",
                    'legacy-' || r.""Id""::text AS ""Slug"",
                    r.""Title"" AS ""Name"",
                    COALESCE(LEFT(r.""MarkdownContent"", 240), '') AS ""Summary"",
                    r.""MarkdownContent"" AS ""Body"",
                    COALESCE(r.""Category"", 'discovery') AS ""CategorySlug"",
                    CASE
                        WHEN r.""Tags"" IS NULL OR r.""Tags"" = '' THEN ARRAY[]::text[]
                        ELSE string_to_array(r.""Tags"", ',')
                    END AS ""Tags"",
                    r.""SkillId"" AS ""PrimarySkillId"",
                    r.""SortOrder"",
                    NOW() AT TIME ZONE 'UTC' AS ""CreatedAt"",
                    NOW() AT TIME ZONE 'UTC' AS ""UpdatedAt""
                FROM ""ReferenceMaterials"" r
                WHERE NOT EXISTS (SELECT 1 FROM ""Techniques"" t WHERE t.""Id"" = r.""Id"");

                INSERT INTO ""Techniques"" (
                    ""Id"", ""Slug"", ""Name"", ""Summary"", ""Body"",
                    ""CategorySlug"", ""Tags"", ""PrimarySkillId"", ""SortOrder"",
                    ""CreatedAt"", ""UpdatedAt""
                ) VALUES
                (
                    gen_random_uuid(),
                    'spin-questions',
                    'SPIN-вопросы',
                    'Четыре типа вопросов, которые ведут клиента к осознанию ценности: Ситуация, Проблема, Импликация, Need-payoff.',
                    'SPIN — фреймворк Нила Рэкхема для консультативных продаж. Situation → Problem → Implication → Need-payoff.',
                    'discovery',
                    ARRAY['discovery', 'B2B'],
                    NULL,
                    1,
                    NOW() AT TIME ZONE 'UTC',
                    NOW() AT TIME ZONE 'UTC'
                ),
                (
                    gen_random_uuid(),
                    'anchor-pricing',
                    'Якорение цены',
                    'Начните диалог о цене с «высокой» точки отсчёта — все последующие цифры будут казаться мягче.',
                    'Anchoring — когнитивное смещение: первая озвученная цифра становится точкой отсчёта для восприятия последующих.',
                    'negotiation',
                    ARRAY['цена', 'пси'],
                    NULL,
                    2,
                    NOW() AT TIME ZONE 'UTC',
                    NOW() AT TIME ZONE 'UTC'
                ),
                (
                    gen_random_uuid(),
                    'feel-felt-found',
                    'Feel · Felt · Found',
                    'Классика для смягчения возражений через эмпатию и социальное доказательство.',
                    'Feel — я понимаю, как вы это чувствуете. Felt — другие клиенты чувствовали то же. Found — и вот что они обнаружили.',
                    'objections',
                    ARRAY['эмпатия'],
                    NULL,
                    3,
                    NOW() AT TIME ZONE 'UTC',
                    NOW() AT TIME ZONE 'UTC'
                ),
                (
                    gen_random_uuid(),
                    'assumptive-close',
                    'Assumptive Close',
                    'Задавайте не «согласны?», а «что удобнее — вторник или четверг?». Решение становится деталью, а не выбором.',
                    'Assumptive Close — закрытие через предположение, что решение уже принято. Переводит фокус с «да/нет» на детали внедрения.',
                    'closing',
                    ARRAY['закрытие'],
                    NULL,
                    4,
                    NOW() AT TIME ZONE 'UTC',
                    NOW() AT TIME ZONE 'UTC'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechniqueCases");

            migrationBuilder.DropTable(
                name: "TechniqueCategories");

            migrationBuilder.DropTable(
                name: "TechniqueCoaches");

            migrationBuilder.DropTable(
                name: "TechniqueDialogTurns");

            migrationBuilder.DropTable(
                name: "TechniqueSkills");

            migrationBuilder.DropTable(
                name: "UserTechniqueProgress");

            migrationBuilder.DropTable(
                name: "Techniques");
        }
    }
}
