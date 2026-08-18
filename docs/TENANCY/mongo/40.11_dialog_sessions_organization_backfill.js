// Sellevate — Phase 40.11: give every existing dialog session an owning organization, and index it.
//
// NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
// once, against a copy of production first and then against production, inside the maintenance
// window described in docs/MICROSERVICES_PRODUCTION_MIGRATION.md ("Раскатка тенантов").
// See docs/DONT_FORGET.md → "Раскатка organization_id в ai-service (блок 40.11)".
//
// WHY THIS IS DIFFERENT FROM THE POSTGRES BACKFILLS
//
//   Postgres fails closed on its own: 40.10's rows were hidden by row-level security until the
//   backfill ran, which is unpleasant but safe. Mongo has no RLS. Between the deploy and this
//   script, a session document with no `organizationId` matches no organization's filter — so it
//   is invisible, not leaked. That is the same fail-closed shape, achieved by the application
//   filter in DialogSessionRepository rather than by the database. It still means "my history
//   disappeared" to a logged-in user, so do not leave the window open.
//
// ORDER
//   1. Deploy ai-service on the new code (its EF migration 20260815154837_AddOrganizationId
//      handles Postgres; it does not touch Mongo).
//   2. Run THIS file.
//   3. Optionally run docs/TENANCY/sql/40.11_ai_organization_indexes_concurrently.sql — Postgres
//      side, performance only, safe with the service running.
//
// INVOCATION (scripts/tenancy-ai-organization-rollout.sh does this for you)
//
//     mongosh "$MONGO_URI" \
//       --eval 'var ORGANIZATION_ID = "00000000-0000-4000-8000-000000000001", APPLY = false' \
//       docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js
//
//   ORGANIZATION_ID must be the SAME id the 40.9 and 40.10 scripts used — ai-service has no tenant
//   registry of its own to look it up in, that is organization-service's job.
//   APPLY defaults to false: without it the script counts and prints, and writes nothing.
//
// Idempotent: documents that already carry an `organizationId` are never touched, so a re-run
// after a partial failure finishes the job instead of re-pointing anything.

/* global db, ORGANIZATION_ID, APPLY, print, printjson */

(function () {
    "use strict";

    const COLLECTION_NAME = "dialog_sessions";
    const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

    const organizationId = typeof ORGANIZATION_ID === "string" ? ORGANIZATION_ID.trim() : "";
    const apply = typeof APPLY !== "undefined" && APPLY === true;

    if (!UUID_PATTERN.test(organizationId)) {
        throw new Error(
            "ORGANIZATION_ID must be a hyphenated UUID matching the one used by the 40.9 backfill. " +
            "Pass it with --eval 'var ORGANIZATION_ID = \"...\"'."
        );
    }

    // The application stores Guids in this collection as strings (BsonRepresentation.String on
    // DialogSession.UserId / OrganizationId), so the backfilled value must be a string too. Writing
    // a BSON UUID here would produce documents the driver deserializes but the filter never
    // matches — invisible sessions with no error anywhere.
    const sessions = db.getCollection(COLLECTION_NAME);

    const orphanFilter = {
        $or: [
            { organizationId: { $exists: false } },
            { organizationId: null },
            { organizationId: "" }
        ]
    };

    const totalCount = sessions.countDocuments({});
    const orphanCount = sessions.countDocuments(orphanFilter);
    const foreignCount = sessions.countDocuments({
        organizationId: { $exists: true, $nin: [null, "", organizationId] }
    });

    print("--- " + COLLECTION_NAME + " ---");
    print("  documents total          : " + totalCount);
    print("  without an organization  : " + orphanCount);
    print("  already owned by another : " + foreignCount);

    if (foreignCount > 0 && orphanCount > 0) {
        // Two different organizations in one database means somebody already ran a rollout with a
        // different id, or this is not the database you think it is. Refuse rather than merge.
        throw new Error(
            "Refusing to backfill: " + foreignCount + " session(s) already belong to a different " +
            "organization than " + organizationId + ". Check ORGANIZATION_ID against the 40.9 run."
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
        const result = sessions.updateMany(orphanFilter, { $set: { organizationId: organizationId } });
        print("  updated                  : " + result.modifiedCount);
    }

    const remaining = sessions.countDocuments(orphanFilter);
    if (remaining !== 0) {
        throw new Error(
            "Backfill incomplete: " + remaining + " session(s) still have no organizationId. " +
            "Re-run; the script is idempotent."
        );
    }

    // Indexes come after the backfill so the build sees final values. Mongo builds indexes in the
    // foreground of the collection but does not block readers/writers on a modern replica set;
    // still, run it in the maintenance window with the rest.
    //
    // Every index leads with organizationId. That is not only for speed: it is the prefix a future
    // shard key must reuse (roadmap 40.11), because a shard key that does not start with the tenant
    // would scatter one customer's sessions across every shard and make a cross-tenant scan the
    // cheap operation.
    sessions.createIndex(
        { organizationId: 1, userId: 1, createdAt: -1 },
        { name: "organizationId_userId_createdAt" }
    );
    sessions.createIndex(
        { organizationId: 1, userId: 1, voiceSeconds: 1 },
        { name: "organizationId_userId_voiceSeconds" }
    );
    sessions.createIndex(
        { organizationId: 1, status: 1, createdAt: -1 },
        { name: "organizationId_status_createdAt" }
    );

    print("");
    print("ai-service dialog sessions: every document is owned and every index leads with the organization.");
    printjson(sessions.getIndexes().map(function (index) { return index.name; }));

    function printIndexPlan() {
        print("  index: organizationId_userId_createdAt");
        print("  index: organizationId_userId_voiceSeconds");
        print("  index: organizationId_status_createdAt");
    }
})();
