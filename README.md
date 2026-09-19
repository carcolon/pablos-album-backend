# BabyAlbum Backend

.NET 10 API for Pablo's private family album. This project is prepared for Render, not Azure.

## Structure

- `src/BabyAlbum.Domain`: album, page, photo and memory domain model.
- `src/BabyAlbum.Application`: use cases, DTO mapping and repository ports.
- `src/BabyAlbum.Infrastructure`: current in-memory repository, ready for EF Core and storage adapters.
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
- `GET /api/albums`
- `GET /api/albums/{albumId}`
- `POST /api/albums/{albumId}/photos` with `multipart/form-data` field `file`

## Google Drive storage

Create a Google Cloud service account with Drive API enabled. Share the target Google Drive folder with the service account email, then configure these environment variables in Render:

```text
GOOGLE_DRIVE_FOLDER_ID=10Vsq5UkIMBeGZVO4HzjdGQJi91mznuku
GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON=<full service account JSON>
CORS_ALLOWED_ORIGINS=<your frontend Render URL>
```

Local development can also use:

```text
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
  - `GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON=<full service account JSON>`
  - `CORS_ALLOWED_ORIGINS=https://your-frontend.onrender.com`

After creating the Google service account, copy its `client_email` and share the Drive folder with that email as Editor. Without that share, Google Drive will reject uploads even if the folder ID is correct.

## Next backend steps

1. Add identity, roles and invitation token persistence.
2. Replace the in-memory repository with EF Core and a relational database.
3. Persist uploaded photo metadata after Drive upload.
4. Add derivative generation and EXIF stripping.
5. Add authorization, architecture and integration tests.
