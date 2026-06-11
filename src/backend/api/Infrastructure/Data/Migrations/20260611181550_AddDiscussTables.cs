using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscussTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsCurated = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscussThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    UpvoteCount = table.Column<int>(type: "integer", nullable: false),
                    ReplyCount = table.Column<int>(type: "integer", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedReplyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsHot = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussThreads_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    UpvoteCount = table.Column<int>(type: "integer", nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussReplies_DiscussThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "DiscussThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscussReplies_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussThreadTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussThreadTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussThreadTags_DiscussTags_TagId",
                        column: x => x.TagId,
                        principalTable: "DiscussTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscussThreadTags_DiscussThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "DiscussThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussReplies_AuthorId",
                table: "DiscussReplies",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussReplies_ThreadId_CreatedAt",
                table: "DiscussReplies",
                columns: new[] { "ThreadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussTags_IsCurated",
                table: "DiscussTags",
                column: "IsCurated");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussTags_Slug",
                table: "DiscussTags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussThreads_AuthorId",
                table: "DiscussThreads",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussThreads_CreatedAt",
                table: "DiscussThreads",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussThreads_IsPinned",
                table: "DiscussThreads",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussThreads_LastActivityAt",
                table: "DiscussThreads",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussThreads_UpvoteCount",
                table: "DiscussThreads",
                column: "UpvoteCount");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussThreadTags_TagId",
                table: "DiscussThreadTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussThreadTags_ThreadId_TagId",
                table: "DiscussThreadTags",
                columns: new[] { "ThreadId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussVotes_TargetType_TargetId",
                table: "DiscussVotes",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussVotes_UserId_TargetType_TargetId",
                table: "DiscussVotes",
                columns: new[] { "UserId", "TargetType", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscussReplies");

            migrationBuilder.DropTable(
                name: "DiscussThreadTags");

            migrationBuilder.DropTable(
                name: "DiscussVotes");

            migrationBuilder.DropTable(
                name: "DiscussTags");

            migrationBuilder.DropTable(
                name: "DiscussThreads");
        }
    }
}
