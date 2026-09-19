# BabyAlbum Backend

.NET 10 API for Pablo's private family album. This project is prepared for Render, not Azure.

## Structure

- `src/BabyAlbum.Domain`: album, page, photo and memory domain model.
- `src/BabyAlbum.Application`: use cases, DTO mapping and repository ports.
- `src/BabyAlbum.Infrastructure`: EF Core Identity/PostgreSQL persistence, current in-memory album repository and storage adapters.
- `src/BabyAlbum.API`: HTTP API endpoints.
- `src/BabyAlbum.Contracts`: DTOs shared across API boundaries.

## Run

```powershell
dotnet run --project src/BabyAlbum.API/BabyAlbum.API.csproj --urls http://localhost:5152
```

## Verify

```powershell
dotnet build BabyAlbum.slnx
```

Useful endpoints:

- `GET /api/health`
- `GET /api/security/csrf`
- `GET /api/auth/status`
- `POST /api/auth/register-owner`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/albums` public album viewer
- `GET /api/albums/{albumId}` public album viewer
- `POST /api/albums/{albumId}/photos` authenticated Owner/Editor with `multipart/form-data` field `file`

## PostgreSQL and native auth

Authentication is native ASP.NET Core Identity with secure cookies, password hashing managed by Identity, PostgreSQL through EF Core, CSRF validation for unsafe API methods and security headers including CSP. The album viewer is public; authentication is required for Studio actions such as uploads and future editing.

Use Neon for the free PostgreSQL database. In local development, the API automatically reads `.env.local` from the backend folder or a parent folder. You can also apply migrations manually with:

```powershell
$env:DATABASE_URL = (Get-Content .env.local | Where-Object { $_ -like 'DATABASE_URL=*' } | Select-Object -First 1).Substring('DATABASE_URL='.Length).Trim().Trim('"').Trim("'")
dotnet ef database update --project src/BabyAlbum.Infrastructure --startup-project src/BabyAlbum.API
```

Render needs this backend environment variable:

```text
DATABASE_URL=<Neon pooled connection string>
```

If there is no Owner yet, the first successful Studio login creates that user as Owner. After one Owner exists, `POST /api/auth/login` only signs in existing users.

## Google Drive storage

For a personal Google Drive folder, use OAuth credentials for the Google account that owns the folder. Service accounts cannot upload into a normal personal "My Drive" folder because they do not have storage quota; they are only suitable for Shared Drives or Workspace setups.

Configure these variables in Render:

```text
GOOGLE_DRIVE_FOLDER_ID=10Vsq5UkIMBeGZVO4HzjdGQJi91mznuku
GOOGLE_DRIVE_OAUTH_CLIENT_ID=<Google OAuth desktop/web client id>
GOOGLE_DRIVE_OAUTH_CLIENT_SECRET=<Google OAuth client secret>
GOOGLE_DRIVE_OAUTH_REFRESH_TOKEN=<Google OAuth refresh token>
GOOGLE_DRIVE_OAUTH_USER=cfca5@hotmail.com
CORS_ALLOWED_ORIGINS=<your frontend Render URL>
```

For Shared Drive or Workspace setups, service-account credentials are still supported:

```text
GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON=<full service account JSON>
GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON_PATH=C:\path\to\service-account.json
```

The browser never receives Google credentials or public Drive URLs. Uploads go through the API and are stored by the `GoogleDriveMediaStorage` adapter.

## Image compression

The upload pipeline accepts JPG, PNG and WebP. If an uploaded image is larger than 10 MB, `ImageSharpImageProcessor` converts it to JPEG and lowers quality/resolution until the stored file is at or below 10 MB. The current intake limit is 50 MB.

## Render notes

Render provides a `PORT` environment variable. The API reads it automatically and binds to `http://0.0.0.0:{PORT}`.

Suggested Render backend settings:

- Service type: Web Service
- Runtime: Docker
- Dockerfile path: `Dockerfile`
- Health check path: `/api/health`
- Environment variables:
  - `GOOGLE_DRIVE_FOLDER_ID=10Vsq5UkIMBeGZVO4HzjdGQJi91mznuku`
  - `GOOGLE_DRIVE_OAUTH_CLIENT_ID=<Google OAuth client id>`
  - `GOOGLE_DRIVE_OAUTH_CLIENT_SECRET=<Google OAuth client secret>`
  - `GOOGLE_DRIVE_OAUTH_REFRESH_TOKEN=<Google OAuth refresh token>`
  - `CORS_ALLOWED_ORIGINS=https://your-frontend.onrender.com`
  - `DATABASE_URL=<Neon pooled connection string>`

## Next backend steps

1. Replace the in-memory album repository with EF Core tables.
2. Persist uploaded photo metadata after Drive upload.
3. Add invitation token persistence for Viewer/Editor users.
4. Add derivative generation and EXIF stripping.
5. Add authorization, architecture and integration tests.
