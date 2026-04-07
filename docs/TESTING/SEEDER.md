# Testing — Content Seeder

## Scope

`POST /admin/seeder/csv` — CSV bulk import of skills, lessons, exercises.

---

## Manual test checklist

### Setup
- Log in as Admin or SuperAdmin (see ADMIN_PANEL.md for default credentials).
- Navigate to Admin Panel → **Seeder**.

### Happy path

| # | Action | Expected result |
|---|---|---|
| 1 | Download template via "Download template" button | `seeder_template.csv` downloaded with 2 example rows |
| 2 | Upload the template | 200 OK; result shows `skillsCreated=1, lessonsCreated=1, exercisesCreated=2` (or similar) |
| 3 | Upload the same template again | Result shows `skillsUpdated=1, lessonsUpdated=1, exercisesUpdated=2` — no duplicates |
| 4 | Navigate to Admin → Skills | Skill "Cold Calling" is present |
| 5 | Open the skill → lesson "Opening the Call" → exercises | Two exercises visible with correct types |

### Validation errors

| # | File | Expected |
|---|---|---|
| 6 | Submit with no file selected | Import button is disabled |
| 7 | Upload a `.txt` file | 400 — "Only .csv files are accepted" |
| 8 | Upload a CSV missing `lesson_title` column | 400 — "Missing columns: lesson_title" |
| 9 | Upload a CSV with a row where `exercise_content_json` is `{bad json` | 200 with `errors: ["Row N: exercise_content_json is not valid JSON."]`; other rows still processed |
| 10 | Upload a CSV with `lesson_difficulty` = `"hard"` (non-integer) | Row-level error collected; other rows still processed |

### Authorization

| # | Scenario | Expected |
|---|---|---|
| 11 | Call `POST /admin/seeder/csv` without `Authorization` header | 401 |
| 12 | Call with a regular User token | 403 |
| 13 | Call with Admin token | 200 |
| 14 | Call with SuperAdmin token | 200 |

---

## Integration test outline (xUnit)

Add to `src/backend/tests/Integration/AdminSeederTests.cs`:

```csharp
[Fact]
public async Task ImportCsv_ValidFile_CreatesSkillsLessonsExercises()
{
    // Arrange: authenticate as admin, prepare minimal valid CSV
    // Act: POST /admin/seeder/csv with multipart form
    // Assert: response 200, skillsCreated >= 1, lessonsCreated >= 1, exercisesCreated >= 1
    //         verify via GET /admin/skills that skill exists
}

[Fact]
public async Task ImportCsv_SameFileTwice_Upserts()
{
    // Act: import same CSV twice
    // Assert: second import returns skillsUpdated >= 1, skillsCreated == 0
    //         row count in DB unchanged
}

[Fact]
public async Task ImportCsv_MissingColumn_Returns400()
{
    // Arrange: CSV without "lesson_title" column
    // Assert: 400 with message containing "Missing columns"
}

[Fact]
public async Task ImportCsv_InvalidJson_ReturnsErrorInResult()
{
    // Arrange: CSV with malformed exercise_content_json on one row
    // Assert: 200, errors list non-empty, other rows processed
}

[Fact]
public async Task ImportCsv_Unauthorized_Returns401()
{
    // Act: no auth header
    // Assert: 401
}

[Fact]
public async Task ImportCsv_UserRole_Returns403()
{
    // Act: call with regular user JWT
    // Assert: 403
}
```

---

## curl example

```bash
# Replace TOKEN with a valid admin JWT
curl -X POST http://localhost:5000/admin/seeder/csv \
  -H "Authorization: Bearer TOKEN" \
  -F "file=@/path/to/content.csv"
```

Expected response:
```json
{
  "skillsCreated": 1,
  "skillsUpdated": 0,
  "lessonsCreated": 1,
  "lessonsUpdated": 0,
  "exercisesCreated": 2,
  "exercisesUpdated": 0,
  "errors": []
}
```
