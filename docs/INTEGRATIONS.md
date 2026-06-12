# External Integrations

## MinIO / S3 Object Storage

Used for storing user avatar images (and any future binary assets).

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
