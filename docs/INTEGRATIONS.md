# External Integrations

## MailerSend (transactional email)

Used to deliver the registration verification code — see
[EMAIL_VERIFICATION.md](EMAIL_VERIFICATION.md). MailerSend is EU-hosted, with a free tier
sufficient for low-volume verification email.

Transport is `Infrastructure/Email/Implementation/MailerSendEmailSender.cs` (implements
`IEmailSender`), which POSTs to `{BaseUrl}/v1/email` (`https://api.mailersend.com`) with a
Bearer API token. The `from` address must belong to a domain verified in MailerSend.

### Setup

1. Create an account at https://app.mailersend.com and add your domain.
2. Configure the DNS records MailerSend shows (SPF, DKIM, and a Return-Path/DMARC record) at
   your domain registrar, and wait for verification — this is what lets mail be sent from
   `noreply@yourdomain`.
3. Create an API token (Domains → your domain → API tokens) and set the env vars below.

### Local development

When `MAILERSEND_API_TOKEN` is left at its placeholder, `MailerSendEmailSender` **skips the
HTTP call and logs the message** (including the code) at Warning level, so registration is
testable locally without an account.

### Environment Variables

| Variable | Maps to | Description |
|----------|---------|-------------|
| `MAILERSEND_API_TOKEN` | `MailerSend:ApiToken` | API token (secret) |
| `MAILERSEND_FROM_EMAIL` | `MailerSend:FromEmail` | Sender address on a verified domain |
| `MAILERSEND_FROM_NAME` | `MailerSend:FromName` | Sender display name (defaults to `Sellevate`) |

Non-secret tuning (`EmailVerification:*` — code length, lifetime, max attempts, resend
cooldown) lives in `appsettings.json`. See [CONFIGURATION.md](CONFIGURATION.md).

## MinIO / S3 Object Storage

Used for storing user avatar images and Discuss photo attachments (and any future binary assets).

### Object key layout (shared `salestrainer-avatars` bucket)

| Key prefix | Owner | Set by |
|------------|-------|--------|
| `defaults/avatar-{NN}.png` | Bundled default avatars | `DefaultAvatarSeeder` |
| `users/{userId}/avatar{ext}` | Uploaded user avatar | `POST /avatars` |
| `discuss/threads/{threadId}/{photoId}{ext}` | Discuss thread photo | `POST /discuss/threads/{threadId}/photos` |
| `discuss/replies/{replyId}/{photoId}{ext}` | Discuss reply photo | `POST /discuss/replies/{replyId}/photos` |

Discuss photo attachments share the avatars bucket under the `discuss/` prefix — see
[DISCUSS.md](DISCUSS.md#photos) and [API_CONTRACTS.md](API_CONTRACTS.md#photos).

### Local Development

| Item | Value |
|------|-------|
| Service | MinIO (S3-compatible) |
| API endpoint | `http://localhost:9000` |
| Console UI | `http://localhost:9001` |
| Default bucket | `salestrainer-avatars` |
| Credentials | `minioadmin` / `minioadmin` (from `appsettings.Development.json`) |

MinIO runs as a Docker service in both `docker-compose.yml` (full stack) and
`docker-compose.infra.yml` (local dev infra stack). Start it with:

```bash
scripts/dev-up.sh           # local dev (infra only in Docker)
# or
docker compose up -d minio  # standalone
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `MINIO_ROOT_USER` | MinIO root user (maps to S3 access key in full-Docker stack) |
| `MINIO_ROOT_PASSWORD` | MinIO root password (maps to S3 secret key in full-Docker stack) |
| `Storage__S3__Endpoint` | S3-compatible endpoint URL |
| `Storage__S3__AccessKey` | Access key |
| `Storage__S3__SecretKey` | Secret key |
| `Storage__S3__Bucket` | Target bucket name |
| `Storage__S3__Region` | AWS region (default `us-east-1`, used for auth signing) |
| `Storage__S3__ForcePathStyle` | Use path-style URLs (required for MinIO, default `true`) |

### Config Section

```json
"Storage": {
  "S3": {
    "Endpoint": "http://localhost:9000",
    "Bucket": "salestrainer-avatars",
    "Region": "us-east-1",
    "ForcePathStyle": true,
    "AccessKey": "INJECTED_FROM_ENV",
    "SecretKey": "INJECTED_FROM_ENV"
  }
}
```

Non-secret defaults live in `appsettings.json`. Development defaults (with
`minioadmin` credentials) live in `appsettings.Development.json`. Secrets are
injected via env vars using the `Storage__S3__*` double-underscore convention
(ASP.NET Core maps `__` to `:` in config keys).

### Production / AWS

For production, point `Storage__S3__Endpoint` at the real AWS S3 endpoint
(or omit it to use the SDK default), set `ForcePathStyle` to `false`, and
supply real IAM credentials via `Storage__S3__AccessKey` /
`Storage__S3__SecretKey` (or use IAM instance roles by leaving them empty and
removing the `BasicAWSCredentials` constructor — requires a code change).

### Code Locations

| Path | Description |
|------|-------------|
| `src/backend/api/Infrastructure/Configuration/S3Configuration.cs` | Options POCO bound from `Storage:S3` |
| `src/backend/api/Infrastructure/Storage/Abstract/IObjectStorage.cs` | Storage abstraction interface |
| `src/backend/api/Infrastructure/Storage/Implementation/S3ObjectStorage.cs` | AWS SDK S3 implementation |
| `src/backend/api/Features/Avatars/AvatarsServiceCollectionExtensions.cs` | DI registration (`AddAvatarStorage`) |

## Default Avatar Seeder

At startup (after `Database.Migrate()`), `DefaultAvatarSeeder.SeedAsync()` runs idempotently:

- Reads 6 tiny PNG files from `SeedAssets/` (copied to build output via `.csproj` `<Content CopyToOutputDirectory>`).
- For each index `i` in `[0, DefaultAvatarCount)`, uploads `defaults/avatar-{i:00}.png` to object storage if not already present (`IObjectStorage.ExistsAsync` guard), then upserts a `DefaultAvatars` DB row by `Index == i`.
- Re-runs are safe: no duplicate DB rows, no redundant object store puts.
- If the object store is unreachable at boot, a warning is logged and startup continues.

### Deterministic avatar assignment

When a new user is created (email registration or Google sign-in), `DefaultAvatarIndexResolver.Resolve(userId, DefaultAvatarSeeder.DefaultAvatarCount)` assigns `User.DefaultAvatarIndex`. The resolver derives a stable, non-negative index from the first 4 bytes of the user's Guid as a `uint`, then takes `% catalogSize`. This means every user gets a consistent default avatar even before the seeder has run.

| File | Role |
|------|------|
| `src/backend/api/Features/Avatars/DefaultAvatarIndexResolver.cs` | Pure static resolver; no dependencies |
| `src/backend/api/Features/Avatars/DefaultAvatarSeeder.cs` | Startup seeder; wired in `Program.cs` |
| `src/backend/api/SeedAssets/avatar-{00..05}.png` | Bundled 1×1 PNG images (6 colors) |
