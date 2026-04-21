using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReshapeTechniques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechniqueCases");

            migrationBuilder.DropTable(
                name: "TechniqueCategories");

            migrationBuilder.DropTable(
                name: "TechniqueDialogTurns");

            migrationBuilder.DropIndex(
                name: "IX_Techniques_CategorySlug",
                table: "Techniques");

            migrationBuilder.DropColumn(
                name: "CategorySlug",
                table: "Techniques");

            migrationBuilder.AddColumn<string>(
                name: "CaseJson",
                table: "Techniques",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DialogJson",
                table: "Techniques",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "Techniques",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_PrimarySkillId",
                table: "Techniques",
                column: "PrimarySkillId");

            migrationBuilder.Sql(@"
                DELETE FROM ""Techniques"";
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Techniques""
                    (""Id"", ""Slug"", ""Name"", ""Summary"", ""Body"", ""Tags"", ""PrimarySkillId"",
                     ""Difficulty"", ""DialogJson"", ""CaseJson"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"")
                SELECT
                    gen_random_uuid(),
                    'spin-questions',
                    'SPIN-вопросы',
                    'Четыре типа вопросов, которые ведут клиента к осознанию ценности: Ситуация, Проблема, Импликация, Need-payoff.',
                    '## Как применять\n\nЗадавайте вопросы в порядке **S → P → I → N**, чтобы клиент сам пришёл к выводу о ценности решения.',
                    ARRAY['discovery', 'B2B']::text[],
                    s.""Id"",
                    3,
                    $$[
                        {""orderIndex"": 0, ""side"": ""them"", ""text"": ""У нас всё хорошо с текущим решением. Спасибо."", ""annotations"": []},
                        {""orderIndex"": 1, ""side"": ""me"",   ""text"": ""Понимаю. Что для вас сейчас сработало лучше всего?"", ""annotations"": [{""label"": ""S"", ""tone"": ""rust""}]},
                        {""orderIndex"": 2, ""side"": ""them"", ""text"": ""Ну, по сути — скорость внедрения."", ""annotations"": []},
                        {""orderIndex"": 3, ""side"": ""me"",   ""text"": ""А что становится узким местом, когда надо масштабировать?"", ""annotations"": [{""label"": ""P → I"", ""tone"": ""rust""}]},
                        {""orderIndex"": 4, ""side"": ""them"", ""text"": ""Честно, в прошлом квартале мы не успели на 2 клиентов."", ""annotations"": []}
                    ]$$::jsonb,
                    $${
                        ""title"": ""Mid-market CRM, 2024"",
                        ""body"": ""Продавец N вошёл в раунд с тёплым лидом, который «посмотрит позже». Три SPIN-вопроса — и клиент сам назвал дату внедрения."",
                        ""metrics"": {""deal"": ""4.2M ₽"", ""cycleDays"": 11}
                    }$$::jsonb,
                    10,
                    now(),
                    now()
                FROM ""Skills"" s WHERE s.""IconicName"" = 'needs-discovery';
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Techniques""
                    (""Id"", ""Slug"", ""Name"", ""Summary"", ""Body"", ""Tags"", ""PrimarySkillId"",
                     ""Difficulty"", ""DialogJson"", ""CaseJson"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"")
                SELECT
                    gen_random_uuid(),
                    'anchor-pricing',
                    'Якорение цены',
                    'Начните диалог о цене с «высокой» точки отсчёта — все последующие цифры будут казаться мягче.',
                    '## Как применять\n\nПервая озвученная цифра становится якорем. Клиент подсознательно сравнивает с ней все остальные цены.',
                    ARRAY['цена', 'пси']::text[],
                    s.""Id"",
                    2,
                    NULL,
                    NULL,
                    20,
                    now(),
                    now()
                FROM ""Skills"" s WHERE s.""IconicName"" = 'price-negotiation';
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Techniques""
                    (""Id"", ""Slug"", ""Name"", ""Summary"", ""Body"", ""Tags"", ""PrimarySkillId"",
                     ""Difficulty"", ""DialogJson"", ""CaseJson"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"")
                SELECT
                    gen_random_uuid(),
                    'feel-felt-found',
                    'Feel · Felt · Found',
                    'Классика для смягчения возражений через эмпатию и социальное доказательство.',
                    '## Как применять\n\n1. **Feel** — признайте чувство клиента.\n2. **Felt** — расскажите, что другие чувствовали так же.\n3. **Found** — покажите, что они нашли после применения решения.',
                    ARRAY['эмпатия']::text[],
                    s.""Id"",
                    3,
                    $$[
                        {""orderIndex"": 0, ""side"": ""them"", ""text"": ""Мне кажется, это дорого."", ""annotations"": []},
                        {""orderIndex"": 1, ""side"": ""me"",   ""text"": ""Понимаю, как это ощущается (Feel)."", ""annotations"": [{""label"": ""Feel"", ""tone"": ""rust""}]},
                        {""orderIndex"": 2, ""side"": ""me"",   ""text"": ""Другие клиенты чувствовали то же самое до внедрения (Felt)."", ""annotations"": [{""label"": ""Felt"", ""tone"": ""rust""}]},
                        {""orderIndex"": 3, ""side"": ""me"",   ""text"": ""И они обнаружили, что цикл сократился на 30% (Found)."", ""annotations"": [{""label"": ""Found"", ""tone"": ""rust""}]}
                    ]$$::jsonb,
                    NULL,
                    30,
                    now(),
                    now()
                FROM ""Skills"" s WHERE s.""IconicName"" = 'objection-handling';
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""Techniques""
                    (""Id"", ""Slug"", ""Name"", ""Summary"", ""Body"", ""Tags"", ""PrimarySkillId"",
                     ""Difficulty"", ""DialogJson"", ""CaseJson"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"")
                SELECT
                    gen_random_uuid(),
                    'assumptive-close',
                    'Assumptive Close',
                    'Задавайте не «согласны?», а «что удобнее — вторник или четверг?». Решение становится деталью, а не выбором.',
                    '## Как применять\n\nПереформулируйте закрытие так, чтобы клиент выбирал деталь, а не факт покупки.',
                    ARRAY['закрытие']::text[],
                    s.""Id"",
                    1,
                    NULL,
                    $${
                        ""title"": ""SaaS, 2024"",
                        ""body"": ""Продавец предложил клиенту выбрать между вариантами оплаты — ежемесячно или годовая подписка. Клиент выбрал годовую, даже не обсуждая сам факт покупки.""
                    }$$::jsonb,
                    40,
                    now(),
                    now()
                FROM ""Skills"" s WHERE s.""IconicName"" = 'closing';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Techniques_PrimarySkillId",
                table: "Techniques");

            migrationBuilder.DropColumn(
                name: "CaseJson",
                table: "Techniques");

            migrationBuilder.DropColumn(
                name: "DialogJson",
                table: "Techniques");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Techniques");

            migrationBuilder.AddColumn<string>(
                name: "CategorySlug",
                table: "Techniques",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TechniqueCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    MetricsJson = table.Column<string>(type: "jsonb", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    TechniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
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
                name: "TechniqueCategories",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechniqueCategories", x => x.Slug);
                });

            migrationBuilder.CreateTable(
                name: "TechniqueDialogTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnnotationsJson = table.Column<string>(type: "jsonb", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TechniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_CategorySlug",
                table: "Techniques",
                column: "CategorySlug");

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueCases_TechniqueId_OrderIndex",
                table: "TechniqueCases",
                columns: new[] { "TechniqueId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TechniqueDialogTurns_TechniqueId_OrderIndex",
                table: "TechniqueDialogTurns",
                columns: new[] { "TechniqueId", "OrderIndex" });
        }
    }
}
