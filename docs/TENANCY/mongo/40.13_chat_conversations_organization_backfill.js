// Sellevate — Phase 40.13: give every existing chat conversation an owning organization, and index it.
//
// NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
// once, against a copy of production first and then against production, inside the maintenance
// window described in docs/MICROSERVICES_PRODUCTION_MIGRATION.md ("Раскатка тенантов").
// See docs/DONT_FORGET.md → "Раскатка organization_id в social-service (блок 40.13)".
//
// WHY THIS IS DIFFERENT FROM THE POSTGRES BACKFILL
//
//   Postgres fails closed on its own: social-db's rows are hidden by row-level security until
//   40.13_social_organization_backfill.sql runs, which is unpleasant but safe. Mongo has no RLS.
//   Between the deploy and this script, a conversation document with no `organizationId` matches no
//   organization's filter — so it is invisible, not leaked. That is the same fail-closed shape,
//   achieved by the application filter in ChatConversationRepository rather than by the database.
//   It still means "my whole chat history disappeared" to a logged-in user, so do not leave the
//   window open. Same reasoning, same shape as ai-service's dialog sessions in 40.11.
//
// ORDER
//   1. Deploy social-service on the new code (its EF migration 20260816081204_AddOrganizationId
//      handles Postgres; it does not touch Mongo).
//   2. Run docs/TENANCY/sql/40.13_social_organization_backfill.sql.
//   3. Run THIS file.
//   4. Optionally run docs/TENANCY/sql/40.13_social_organization_indexes_concurrently.sql —
//      Postgres side, performance only, safe with the service running.
//
// INVOCATION (scripts/tenancy-social-organization-rollout.sh does this for you)
//
//     mongosh "$MONGO_URI" \
//       --eval 'var ORGANIZATION_ID = "00000000-0000-4000-8000-000000000001", APPLY = false' \
//       docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js
//
//   ORGANIZATION_ID must be the SAME id the 40.9–40.12 scripts used — social-service has no tenant
//   registry of its own to look it up in, that is organization-service's job.
//   APPLY defaults to false: without it the script counts and prints, and writes nothing.
//
// Idempotent: documents that already carry an `organizationId` are never touched, so a re-run
// after a partial failure finishes the job instead of re-pointing anything.

/* global db, ORGANIZATION_ID, APPLY, print, printjson */

(function () {
    "use strict";

    const COLLECTION_NAME = "chat_conversations";
    const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

    const organizationId = typeof ORGANIZATION_ID === "string" ? ORGANIZATION_ID.trim() : "";
    const apply = typeof APPLY !== "undefined" && APPLY === true;

    if (!UUID_PATTERN.test(organizationId)) {
        throw new Error(
            "ORGANIZATION_ID must be a hyphenated UUID matching the one used by the 40.9 backfill. " +
            "Pass it with --eval 'var ORGANIZATION_ID = \"...\"'."
        );
    }

    // The application stores this Guid as a string (BsonRepresentation.String on
    // ChatConversation.OrganizationId), matching how participantIds and senderId are already
    // stored. Writing a BSON UUID here would produce documents the driver deserializes but the
    // filter never matches — invisible conversations with no error anywhere.
    const conversations = db.getCollection(COLLECTION_NAME);

    const orphanFilter = {
        $or: [
            { organizationId: { $exists: false } },
            { organizationId: null },
            { organizationId: "" }
        ]
    };

    const totalCount = conversations.countDocuments({});
    const orphanCount = conversations.countDocuments(orphanFilter);
    const foreignCount = conversations.countDocuments({
        organizationId: { $exists: true, $nin: [null, "", organizationId] }
    });

    print("--- " + COLLECTION_NAME + " ---");
    print("  documents total          : " + totalCount);
    print("  without an organization  : " + orphanCount);
    print("  already owned by another : " + foreignCount);

    if (foreignCount > 0 && orphanCount > 0) {
        // Two different organizations in one database means somebody already ran a rollout with a
        // different id, or this is not the database you think it is. Refuse rather than merge —
        // these documents are transcripts of one customer's people talking to each other.
        throw new Error(
            "Refusing to backfill: " + foreignCount + " conversation(s) already belong to a " +
            "different organization than " + organizationId + ". Check ORGANIZATION_ID against the " +
            "40.9 run."
        );
    }

    if (!apply) {
        print("");
        print("DRY RUN — nothing was written. Re-run with --eval 'var APPLY = true' to apply.");
        print("Would set organizationId = " + organizationId + " on " + orphanCount + " document(s),");
        print("then create the compound indexes listed below.");
        printIndexPlan();
        return;
    }

    if (orphanCount > 0) {
        const result = conversations.updateMany(orphanFilter, { $set: { organizationId: organizationId } });
        print("  updated                  : " + result.modifiedCount);
    }

    const remaining = conversations.countDocuments(orphanFilter);
    if (remaining !== 0) {
        throw new Error(
            "Backfill incomplete: " + remaining + " conversation(s) still have no organizationId. " +
            "Re-run; the script is idempotent."
        );
    }

    // Indexes come after the backfill so the build sees final values. Mongo builds indexes in the
    // foreground of the collection but does not block readers/writers on a modern replica set;
    // still, run it in the maintenance window with the rest.
    //
    // Every index leads with organizationId. That is not only for speed: it is the prefix a future
    // shard key must reuse, because a shard key that does not start with the tenant would scatter
    // one customer's conversations across every shard and make a cross-tenant scan the cheap
    // operation.
    //
    // The three shapes match the three filters in ChatConversationRepository exactly — the
    // conversation list, the "one conversation this user is in" lookup, and "the conversation
    // between exactly these two participants". Nothing else queries this collection; the tripwire
    // test in Social.Tests is what keeps that true.
    conversations.createIndex(
        { organizationId: 1, participantIds: 1, lastMessageAt: -1 },
        { name: "organizationId_participantIds_lastMessageAt" }
    );
    conversations.createIndex(
        { organizationId: 1, _id: 1, participantIds: 1 },
        { name: "organizationId_id_participantIds" }
    );
    conversations.createIndex(
        { organizationId: 1, participantIds: 1 },
        { name: "organizationId_participantIds" }
    );

    print("");
    print("social-service chat conversations: every document is owned and every index leads with the organization.");
    printjson(conversations.getIndexes().map(function (index) { return index.name; }));

    function printIndexPlan() {
        print("  index: organizationId_participantIds_lastMessageAt");
        print("  index: organizationId_id_participantIds");
        print("  index: organizationId_participantIds");
    }
})();
