<p align="center">
  <a href="https://www.findmycat.io/">
    <img width="200" height="200" alt="FindMyCat" src="https://github.com/user-attachments/assets/822095bc-fa84-415d-ae35-8c0257899347" />
  </a>
</p>

<p align="center">
    <a href="https://discord.com/invite/63dxeuhfvk">Discord</a>
    ·
    <a href="https://www.findmycat.io/">FindMyCat.io</a>
    ·
    <a href="https://github.com/SeanAkin/FindMyCat-Web/issues">Issues</a>
  </p>
</p>

<h1 align="center">FindMyCat</h1>

<p align="center">
  The Open-Source Pet Tracker
</p>

<p align="center">
  <img width="1920" height="1080" alt="Demo Gif" src="https://github.com/user-attachments/assets/8158da6a-2b1a-4708-9b19-27aedb05d086" />
</p>

## Overview

FindMyCat-Web is an unofficial self-hostable dashboard for managing GPS trackers built on the open-source [FindMyCat.io](https://www.findmycat.io/) project.
It's a .NET/React app that sits in front of a Traccar server (for live positions and location history) and Hologram (for sending commands over cellular).
Use the map-based UI to view tracker history and send commands. You can also manage user access so friends and family can keep track of your pets.

- **Backend**: ASP.NET Core (.NET 10) API, EF Core, cookie-based auth with optional Google OAuth
- **Frontend**: React 19 + TypeScript, Vite, Tailwind CSS, Leaflet
- **Tracking**: Traccar server (bring your own instance)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [FindMyCat.io's Cloud Software](https://www.findmycat.io/docs/CloudSetup) (This project depends on the Traccar element)
- SQLite is bundled, so you do not need to install a separate database for local development

### Clone the repo

```
git clone https://github.com/SeanAkin/FindMyCat-Web.git
cd FindMyCat-Web
```

### Backend

A couple of user secrets are required to start. For local development, the easiest way is [.NET user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```
cd FindMyCat.Api
dotnet user-secrets set "FINDMYCAT_ENCRYPTION_KEY" "$(openssl rand -base64 32)"
dotnet user-secrets set "Traccar:BaseUrl" "https://your-traccar-instance.example.com"
dotnet run
```

The API starts on `http://localhost:5120` (see `FindMyCat.Api/Properties/launchSettings.json`).

### Frontend

```
cd frontend
npm install
npm run dev
```

The dev server runs on `http://localhost:5173`. It proxies `/auth`, `/api`, and `/public` requests to the backend on port 5120 (see `frontend/vite.config.ts`).

Open `http://localhost:5173` and create an account with email and password, or sign in with Google if you've configured it. See [Environment Variables](#environment-variables) below.

### Running tests

```
dotnet test FindMyCat.slnx          # backend unit + integration tests
```

```
cd frontend
npm run test                        # frontend unit tests (Vitest)
npm run e2e                         # end-to-end tests (Cypress)
```

## Deployment

FindMyCat ships as a single Docker image. The frontend is built and copied into the backend's `wwwroot`, so one container serves both the API and the web app.

### Build and run

```
docker build -t findmycat .
docker run -d \
  --name findmycat \
  -p 8080:8080 \
  -e FINDMYCAT_ENCRYPTION_KEY="..." \
  -e Traccar__BaseUrl="https://your-traccar-instance.example.com" \
  -e ConnectionStrings__Default="Data Source=/data/findmycat.db" \
  -v findmycat-data:/data \
  findmycat
```

The container listens on port `8080`.

### Persistent storage

FindMyCat uses SQLite, a single file database, and applies EF Core migrations automatically on startup. The container filesystem is ephemeral. If the database file lives inside the container instead of a mounted volume, every redeploy or restart wipes all users, devices, and history.

Always mount a volume and point `ConnectionStrings__Default` at a path inside it, as in the `docker run` example above:

```
-e ConnectionStrings__Default="Data Source=/data/findmycat.db"
-v findmycat-data:/data
```

Equivalent `docker-compose.yml`:

```yaml
services:
  findmycat:
    build: .
    ports:
      - "8080:8080"
    environment:
      FINDMYCAT_ENCRYPTION_KEY: "..."
      Traccar__BaseUrl: "https://your-traccar-instance.example.com"
      ConnectionStrings__Default: "Data Source=/data/findmycat.db"
      Authentication__Google__Enabled=true
      Authentication__Google__ClientId=your-client-id.apps.googleusercontent.com
      Authentication__Google__ClientSecret=your-client-secret
    volumes:
      - findmycat-data:/data

volumes:
  findmycat-data:
```

Using a platform that manages volumes for you, like Dokploy, Coolify, or a Kubernetes PVC? The requirement doesn't change. Give the container a persistent mount and point the connection string at a file inside it.

### Environment variables

| Variable | Required | Default | Description |
| --- | --- | --- | --- |
| `FINDMYCAT_ENCRYPTION_KEY` | Yes | None | Base64-encoded 32-byte key used to encrypt stored Traccar/Hologram credentials. Generate one with `openssl rand -base64 32`. |
| `Traccar__BaseUrl` | Yes | None | Base URL of your Traccar server, e.g. `https://traccar.example.com`. |
| `ConnectionStrings__Default` | No | `Data Source=findmycat.db` | SQLite connection string. Point this at a mounted volume in production. |
| `Authentication__Google__Enabled` | No | `true` | Set to `false` to disable Google sign-in entirely. This removes the button on the frontend and the provider on the backend. |
| `Authentication__Google__ClientId` | Only if Google auth is enabled | None | OAuth client ID from the Google Cloud Console. |
| `Authentication__Google__ClientSecret` | Only if Google auth is enabled | None | OAuth client secret from the Google Cloud Console. |

Double underscores (`__`) are ASP.NET Core's convention for nested configuration keys. `Traccar__BaseUrl`, for example, maps to `Traccar:BaseUrl` in `appsettings.json`.

Example `.env` file, for use with your own compose or orchestration setup (FindMyCat does not ship one):

```
FINDMYCAT_ENCRYPTION_KEY=NOT_A_REAL_AES_256_KEY
Traccar__BaseUrl=https://traccar.example.com
ConnectionStrings__Default=Data Source=/data/findmycat.db
Authentication__Google__Enabled=true
Authentication__Google__ClientId=your-client-id.apps.googleusercontent.com
Authentication__Google__ClientSecret=your-client-secret
ASPNETCORE_ENVIRONMENT=Production
```
### Key Rotation
The `FINDMYCAT_ENCRYPTION_KEY` can be rotated by updating the env var value and restarting the docker container. This will invalidate the existing values and require you to re-enter them on the admin page. This key is not used for username and passwords which are hashed.

## License

MIT. See [LICENSE](LICENSE).
