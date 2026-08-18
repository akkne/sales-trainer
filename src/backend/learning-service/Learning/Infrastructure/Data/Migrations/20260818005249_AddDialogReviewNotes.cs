using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.25 - two-way feedback on a graded conversation
    /// (docs/TENANCY/ASSIGNMENTS.md 4.1).
    ///
    /// <para>
    /// <b>One table for both directions.</b> A coaching note (ROP to manager) and a score dispute
    /// (manager to ROP) share a session, a quoted fragment, a comment, an author, a subject and a
    /// resolution. Two tables would duplicate all six and give the tenant column, the freeze rules
    /// and the frozen-quote copy two places to be got right. What genuinely differs is who may close
    /// the row and with which word, and that is the per-kind status constraint below rather than a
    /// second schema. Rejected alternatives are in docs/DECISIONS.md (2026-08-18).
    /// </para>
    ///
    /// <para>
    /// <b>Strict tenant data.</b> A conversation and everything said about it happen inside one
    /// organization, so OrganizationId is NOT NULL and the policy is plain equality - never the
    /// content policy's "IS NULL OR = current". A global row here would mean one customer's manager
    /// arguing about a grade in front of every other customer.
    /// </para>
    ///
    /// <para>
    /// <b>No backfill, no maintenance window, no concurrent-index script</b> - the fifth block in a
    /// row to make that call, and for the same reason: the table is created empty by this migration,
    /// so all three indexes are built over zero rows and the ACCESS EXCLUSIVE lock costs nothing.
    /// Nothing could be backfilled either - no coaching note or dispute has ever existed anywhere to
    /// copy from. See docs/DONT_FORGET.md.
    /// </para>
    ///
    /// <para>
    /// <b>SessionId is not a foreign key and never can be</b>: the conversation is a Mongo document
    /// in ai-service. What makes the value trustworthy is that nothing writes it from a request -
    /// every insert copies it from the UserDialogScores row for that session, which is itself under
    /// row-level security, so a session belonging to another organization does not exist to the code
    /// that would write it here.
    /// </para>
    /// </summary>
    public partial class AddDialogReviewNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DialogReviewNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "coaching_note"),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DialogModeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotedFromMessageIndex = table.Column<int>(type: "integer", nullable: true),
                    QuotedToMessageIndex = table.Column<int>(type: "integer", nullable: true),
                    QuotedText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DisputedScore = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "open"),
                    Resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AdjustedScore = table.Column<int>(type: "integer", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogReviewNotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DialogReviewNotes_OrganizationId_Kind_Status_CreatedAt",
                table: "DialogReviewNotes",
                columns: new[] { "OrganizationId", "Kind", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogReviewNotes_OrganizationId_SessionId",
                table: "DialogReviewNotes",
                columns: new[] { "OrganizationId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogReviewNotes_OrganizationId_SubjectUserId_Status",
                table: "DialogReviewNotes",
                columns: new[] { "OrganizationId", "SubjectUserId", "Status" });

            // At most one unreviewed dispute per conversation. Not tidiness: a queue that can be
            // filled with duplicates of one complaint is a queue the ROP stops opening, and the whole
            // mechanism only works while they keep opening it. Partial, so the same conversation may
            // be disputed again after a verdict - a person who was told "the grade stands" and then
            // finds new evidence is not spamming.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_DialogReviewNotes_OpenDisputePerSession"
                    ON "DialogReviewNotes" ("OrganizationId", "SessionId")
                    WHERE "Kind" = 'score_dispute' AND "Status" = 'open';
                """);

            // The vocabulary, and - the load-bearing one - which endings belong to which kind. A
            // coaching note cannot be "upheld" and a dispute cannot be closed by being read, because
            // those two words are what separate a review from an acknowledgement. Kept in the
            // database rather than only in DialogReviewStatuses.IsTerminalFor, so a second writer
            // added later inherits the rule instead of having to remember it.
            //
            // The author/subject rule is the other half, and only in one direction: a dispute is
            // filed by the person whose conversation it is. Without it a row could be recorded as a
            // dispute and land in the ROP's queue carrying somebody else's words as the
            // complainant's, which is the one falsification that would poison the labelled dataset
            // this table doubles as. The reverse is deliberately not asserted - a ROP who also
            // practises may write a note on their own conversation, and refusing it in the database
            // would turn a harmless act into a 500.
            migrationBuilder.Sql("""
                ALTER TABLE "DialogReviewNotes"
                    ADD CONSTRAINT "CK_DialogReviewNotes_Kind"
                    CHECK ("Kind" IN ('coaching_note', 'score_dispute'));

                ALTER TABLE "DialogReviewNotes"
                    ADD CONSTRAINT "CK_DialogReviewNotes_Status"
                    CHECK (
                        ("Kind" = 'coaching_note' AND "Status" IN ('open', 'acknowledged'))
                        OR ("Kind" = 'score_dispute' AND "Status" IN ('open', 'upheld', 'rejected'))
                    );

                ALTER TABLE "DialogReviewNotes"
                    ADD CONSTRAINT "CK_DialogReviewNotes_Author"
                    CHECK ("Kind" <> 'score_dispute' OR "AuthorUserId" = "SubjectUserId");

                ALTER TABLE "DialogReviewNotes"
                    ADD CONSTRAINT "CK_DialogReviewNotes_Comment"
                    CHECK (length(btrim("Comment")) > 0);

                ALTER TABLE "DialogReviewNotes"
                    ADD CONSTRAINT "CK_DialogReviewNotes_SessionId"
                    CHECK (length(btrim("SessionId")) > 0);

                ALTER TABLE "DialogReviewNotes"
                    ADD CONSTRAINT "CK_DialogReviewNotes_Quote"
                    CHECK (
                        ("QuotedFromMessageIndex" IS NULL OR "QuotedFromMessageIndex" >= 0)
                        AND ("QuotedToMessageIndex" IS NULL OR "QuotedToMessageIndex" >= 0)
                        AND (
                            "QuotedFromMessageIndex" IS NULL
                            OR "QuotedToMessageIndex" IS NULL
                            OR "QuotedToMessageIndex" >= "QuotedFromMessageIndex"
                        )
                    );

                ALTER TABLE "DialogReviewNotes"
                    ADD CONSTRAINT "CK_DialogReviewNotes_Scores"
                    CHECK (
                        ("DisputedScore" IS NULL OR ("DisputedScore" >= 0 AND "DisputedScore" <= 100))
                        AND ("AdjustedScore" IS NULL OR ("AdjustedScore" >= 0 AND "AdjustedScore" <= 100))
                        AND ("AdjustedScore" IS NULL OR "Status" = 'upheld')
                    );
                """);

            // A quoted fragment on a coaching note is required by the service and by this
            // constraint. The note's entire product value is the three lines the ROP is taking to
            // Monday's meeting; one that says only "messages 4 to 6" is unreadable the moment the
            // session ages out, and unreadable to the manager immediately.
            migrationBuilder.Sql("""
                ALTER TABLE "DialogReviewNotes"
                    ADD CONSTRAINT "CK_DialogReviewNotes_CoachingNoteQuote"
                    CHECK (
                        "Kind" <> 'coaching_note'
                        OR ("QuotedText" IS NOT NULL AND length(btrim("QuotedText")) > 0)
                    );
                """);

            migrationBuilder.EnableTenantRls("DialogReviewNotes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("DialogReviewNotes");

            // Drops the table's indexes and check constraints with it.
            migrationBuilder.DropTable(
                name: "DialogReviewNotes");
        }
    }
}
