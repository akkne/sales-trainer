# Testing — Demo Data seeder

Feature: [docs/DEMO_DATA.md](../DEMO_DATA.md). Script: `scripts/seed-demo-data.py`.

## Automated

**There is no automated suite, and that is a deliberate scope decision rather than an
omission.** The script's only behaviour is "write these rows into these five running
containers"; testing it in isolation would mean standing up Postgres, Mongo and Redis via
Testcontainers and then asserting on the same INSERTs the script generates — a test that
restates the code and still would not catch the thing that actually breaks it, which is a
schema change in a service. The checks below are therefore run by hand against a live
local stack, and the constraint checks in the databases themselves are what catch a
malformed row: every table this script writes carries CHECK constraints, unique indexes
and RLS policies, and `-v ON_ERROR_STOP=1` turns any violation into a non-zero exit.

A syntax-level smoke check is free and worth running after any edit:

```bash
python3 -m compileall -q scripts/seed-demo-data.py
python3 scripts/seed-demo-data.py --dry-run
```

`--dry-run` exercises every code path except the writes: container discovery, the
organization and owner lookups, the content-library reads, and the full row generation
for all seven batches. A crash in generation surfaces here.

## Manual checklist

Start the infra (`scripts/dev-infra.sh`) and make sure the content library is seeded
(`.claude/local-seed/seed.py`) before any of this.

| # | Step | Expected |
|---|---|---|
| 1 | `python3 scripts/seed-demo-data.py --dry-run` | Prints the resolved containers, organization, owner and content counts, then the plan, then "nothing was written". Exit 0 |
| 2 | `python3 scripts/seed-demo-data.py` | Seven `✓` lines. Mongo reports `dialog sessions inserted: N` with N > 0. Exit 0 |
| 3 | Run it a second time | Identical output except `dialog sessions inserted: 0`. Every row count below is unchanged — this is the idempotency check |
| 4 | Stop the infra and run it | Fails with "No running container for compose service 'postgres'", exit 1 — not a stack trace |
| 5 | `--organization 00000000-0000-0000-0000-000000000000` | `error: No organization with id '…'`, exit 1. The id is checked against the registry before anything is written — nothing here is a foreign key, so an unchecked id would half-succeed and leave rows no screen can read |
| 6 | `--organization nonsense` | `error: 'nonsense' is not a uuid.`, exit 1 |
| 7 | `--owner-email nobody@example.com` | `error: No account 'nobody@example.com'. …`, exit 1 |

### Row counts after a successful run

```bash
docker exec repository-postgres-1 psql -U st -q -d identity -c \
  'SELECT (SELECT count(*) FROM "Users") users, (SELECT count(*) FROM "Memberships") memberships;'

docker exec repository-postgres-1 psql -U st -q -d learning -c \
  'SELECT (SELECT count(*) FROM "UserExerciseAttempts") attempts,
          (SELECT count(*) FROM "UserDialogScores") dialog_scores,
          (SELECT count(*) FROM "Assignments") assignments,
          (SELECT count(*) FROM "AssignmentProgressRecords") assignment_progress,
          (SELECT count(*) FROM "DialogReviewNotes") review_notes,
          (SELECT count(*) FROM "SkillStages") stages;'

docker exec repository-postgres-1 psql -U st -q -d company -c \
  'SELECT (SELECT count(*) FROM "Companies") companies,
          (SELECT count(*) FROM "CompanyContacts") contacts,
          (SELECT count(*) FROM "CallLogEntries") call_log;'

docker exec repository-postgres-1 psql -U st -q -d social -c \
  'SELECT (SELECT count(*) FROM "DiscussThreads") threads,
          (SELECT count(*) FROM "DiscussReplies") replies,
          (SELECT count(*) FROM "DiscussVotes") votes;'

docker exec repository-mongo-1 mongosh sallevate --quiet \
  --eval 'print(db.dialog_sessions.countDocuments())'

docker exec repository-redis-1 redis-cli KEYS 'org:*notifications:inbox:*' | wc -l
```

Reference values on a stack whose library holds 6 skills / 22 lessons / 90 exercises:
13 users in the roster, ~880 attempts, 99 dialog scores, 6 assignments, 65 assignment
progress rows, 13 review notes, 5 stages, 16 companies, 31 contacts, 67 call-log entries,
18 threads, 49 replies, 411 votes, 99 new dialog sessions, 13 inboxes. The attempt and
company numbers move with `RANDOM_SEED` and with the size of the library; the structural
ones (13 users, 5 stages, 6 assignments, 18 threads) do not.

### The invariant the whole thing exists for

The team skill map withholds an accuracy below 5 attempts in a cell. If any cell falls
under that, `/org` goes back to showing «нет данных» and the seed has failed at its
actual job:

```bash
docker exec repository-postgres-1 psql -U st -q -d learning -c '
SELECT min(cell_attempts) AS min_cell, count(*) AS cells, count(DISTINCT user_id) AS people
FROM (
  SELECT a."UserId" user_id, s."Id" skill_id, count(*) cell_attempts
  FROM "UserExerciseAttempts" a
  JOIN "Exercises" e ON e."Id" = a."ExerciseId"
  JOIN "Lessons"   l ON l."Id" = e."LessonId"
  JOIN "Topics"    t ON t."Id" = l."TopicId"
  JOIN "Skills"    s ON s."Id" = t."SkillId"
  WHERE a."AttemptedAt" >= now() - interval '"'"'90 days'"'"'
  GROUP BY 1, 2
) cells;'
```

`min_cell` must be ≥ 5 (it is 7 by construction — `MINIMUM_ATTEMPTS_PER_SKILL`), and
`cells` must equal `people × skills` with no gaps.

`/discuss` has a narrower version of the same trap: «Топ авторов недели» counts votes from
the **last 7 days only**, so a dataset whose activity all landed a month ago renders the
panel empty.

```bash
docker exec repository-postgres-1 psql -U st -q -d social -c \
  "SELECT count(*) FILTER (WHERE \"CreatedAt\" >= now() - interval '7 days') AS votes_7d FROM \"DiscussVotes\";"
```

`votes_7d` must be > 0.

### Screens to eyeball

Sign in as any demo account (`alina.kovaleva@sellevate.dev`, same password as the owner
account) and confirm none of these shows its empty state:

- `/org` — heat map with a readable strong/weak pattern, no «нет данных» in the weakest-stage column
- `/org/assignments` — three active, one draft, two closed; funnel bars populated
- `/org/dialogs` — sessions listed, coaching notes and at least one score dispute
- `/companies` and a company card — contacts, call log, briefing, readiness score, follow-up
- `/discuss` — threads, replies, popular tags, top authors of the week. Hidden from the
  navigation, so open it by address; the notification bell must contain **no** «Ответ в
  обсуждении» entry
- `/tree`, `/skill/[id]`, `/profile` — accuracy tiles show percentages, stage groups carry labels and colours
- notification bell — populated inbox, a mix of read and unread
- dialog history sidebar — past conversations with feedback

## Related

- [docs/TESTING/ORG_PANEL.md](ORG_PANEL.md) — the screens this data mostly exists to fill
- [docs/TESTING/DISCUSS.md](DISCUSS.md), [docs/TESTING/COMPANIES.md](COMPANIES.md)
