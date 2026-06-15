# Exercise Content Validation Tests

## Overview

Exercise content is validated server-side on creation, update, and import. The `ExerciseContentValidator` class enforces per-type content schemas.

## Unit Tests

**Location:** `src/backend/tests/Unit/ExerciseContentValidatorTests.cs`

Tests validate the 10 exercise type schemas:
- `choose_option`, `fill_blank` — binary choice with options and correct index
- `reorder`, `match_pairs`, `categorize` — structured arrangement
- `spot_mistake`, `rewrite`, `ai_dialogue`, `evaluate_call`, `free_text` — AI-evaluated types

Each type test covers:
- Valid content (passes validation)
- Missing required fields (rejected)
- Invalid field types (rejected)
- Edge cases (empty arrays, out-of-range indices, etc.)

**Run unit tests:**
```bash
dotnet test src/backend/tests/Unit --filter ExerciseContentValidator
```

## Integration Tests

Exercise content validation is also tested via HTTP endpoints:

**Endpoints tested:**
- `POST /admin/lessons/:lessonId/exercises` — single create with 400 on invalid content
- `PUT /admin/exercises/:id` — single update with 400 on invalid content
- `POST /admin/lessons/:lessonId/exercises/import` — batch import with per-item validation (bad items skipped, errors reported)
- `POST /admin/seeder/lessons` — seeder import with per-item validation

**Run integration tests:**
```bash
dotnet test src/backend/tests/Integration --filter Exercise
```

## Frontend Type Checking

TypeScript schema validation occurs client-side before submission:

**Location:** `src/frontend/features/admin/components/exercise-editors/types.ts`

**Run type check:**
```bash
npx tsc --noEmit
```

This validates all editor components conform to the canonical exercise type schemas (e.g., `choose_option` requires `situation`, `options`, `is_correct` flags, etc.).

## Content Schema Reference

For the exact field requirements per type, see [NEW_EXERCISE_TYPES.md](../NEW_EXERCISE_TYPES.md).

## Manual Testing

When authoring exercises via the admin panel:
1. Edit/create an exercise in the visual editor
2. Leave a required field empty
3. Attempt to save
4. Verify 400 error is returned with a descriptive message

When importing via seeder:
1. Prepare a JSON file with some exercises containing invalid content
2. Upload via `/admin/seeder/lessons`
3. Verify bad items are skipped and errors are reported per exercise
