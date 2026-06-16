# Testing — Discuss Photos

## Scope

Photo attachments (up to 10 per thread or reply): upload, max-count enforcement,
authorization, magic-byte validation, thread-delete cascade, and the `PhotoPicker`
component add/disable guard.

## Backend unit tests

**File:** `src/backend/tests/Unit/DiscussPhotoServiceTests.cs`
**Framework:** NUnit 4 + FluentAssertions + EF Core InMemory
**IObjectStorage:** `InMemoryObjectStorage` — in-process test double (defined at the
bottom of the same file); tracks `PutCallCount` so tests can assert nothing was written
to storage on error paths.

| Test name | Assertion |
|---|---|
| `UploadPhotosAsync_AuthorUploadesTwoValidPngs_ReturnSuccessAndPersistsBothWithCorrectOrder` | Status=Success; 2 DB rows; OrderIndex 0/1; IObjectStorage.Put called twice |
| `UploadPhotosAsync_OwnerAlreadyHasNinePhotos_UploadingTwoReturnsValidationErrorAndPersistsNothing` | Status=ValidationError; still 9 rows; 0 S3 puts |
| `UploadPhotosAsync_NonAuthorUploads_ReturnsForbiddenAndPersistsNothing` | Status=Forbidden; 0 rows; 0 S3 puts |
| `UploadPhotosAsync_BatchContainsFileWithInvalidMagicBytes_ReturnsValidationErrorAndPersistsNothing` | Mixed batch (1 valid PNG + 1 garbage); Status=ValidationError; 0 rows; 0 S3 puts (validate-all-before-put) |
| `DeleteThreadAsync_ThreadHasPhotos_RemovesAllPhotoRows` | After DeleteThreadAsync the DiscussPhotos table is empty |

Run (no Docker required — unit tests use InMemory DB):
```bash
cd src/backend
dotnet test tests/Sellevate.Tests.csproj --filter "FullyQualifiedName~DiscussPhotoService"
```

## Frontend component tests

**File:** `src/frontend/__tests__/DiscussPhotoPicker.test.tsx`
**Framework:** Vitest 3 + React Testing Library + jsdom

| Test name | Assertion |
|---|---|
| `calls onChange with the selected file when a valid image is picked` | Selecting one PNG via the hidden file input calls onChange with an array containing that file |
| `disables the add button when ten photos are already attached` | When `files.length === 10` the "Добавить фото" button has `disabled=true` |

Run:
```bash
cd src/frontend
npm test -- __tests__/DiscussPhotoPicker.test.tsx
```
