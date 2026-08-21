# FindMyCat-Web

![FindMyCat Logo](FindMyCat%20Logo.png)

**An Unofficial Web Application for the [FindMyCat.io](https://findmycat.io) Project**

A web dashboard for viewing the location and status of your FindMyCat pet tracker, built as a community companion to the open-source FindMyCat ecosystem. This project is **not** affiliated with the official FindMyCat project — it is an independent, community-driven web client.

## ✨ Features

- 🗺️ **Map-based location dashboard** — see where your tracker is, powered by [Leaflet](https://leafletjs.com/)
- 🔐 **Google sign-in** — OAuth 2.0 authentication with cookie sessions (ASP.NET Core Cookie + Google provider)
- 📍 **Traccar integration** — pulls device/position data from a self-hosted [Traccar](https://www.traccar.org/) server
- 📶 **Hologram integration** — send commands to trackers through the [Hologram](https://www.hologram.io/) cellular IoT platform (device lookup by IMEI)
- 👤 **Role-based access** — authenticated users with role claims, enforced via a global authorization policy
- 🧩 **Modern React frontend** — React 19, TypeScript, Tailwind CSS 4, shadcn/ui, Zustand
- 🧪 **Tested end-to-end** — unit + integration tests on the backend, Vitest + Cypress on the frontend, CI on every PR

## 🏗️ Tech Stack

| Layer | Tech |
|---|---|
| Backend | ASP.NET Core 10 (C#), EF Core, SQLite |
| Frontend | React 19, Vite 8, TypeScript, Tailwind CSS 4, shadcn/ui, Zustand, Leaflet |
| Auth | Google OAuth 2.0, ASP.NET Core Cookie authentication |
| Integrations | Traccar (GPS), Hologram (cellular IoT) |
| Testing | xUnit (unit + integration via WebApplicationFactory), Vitest, Cypress |
| Tooling | oxlint, Prettier, GitHub Actions |

## 📁 Repository Structure

```
FindMyCat-Web/
├── FindMyCat.Api/               # ASP.NET Core Web API (controllers, contracts, auth setup)
├── FindMyCat.Core/              # Domain entities, services (Traccar, Hologram, user provisioning)
├── FindMyCat.Data/              # EF Core DbContext and persistence
├── FindMyCat.UnitTests/         # Backend unit tests
├── FindMyCat.IntegrationTests/  # Backend integration tests (WebApplicationFactory)
├── frontend/                    # React + Vite + TypeScript SPA
│   ├── src/                     # Application source
│   ├── cypress/                 # E2E tests
│   └── vite.config.ts           # Dev server + API proxy (/api, /auth, /public → :5120)
├── .github/workflows/           # CI (pull-request.yml)
└── FindMyCat.slnx               # .NET solution
```

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 20+ and npm

### 1. Configure the backend

The API requires the following configuration. Environment variables (recommended) or `FindMyCat.Api/appsettings.json`:

| Setting | Required | Description |
|---|---|---|
| `FINDMYCAT_ENCRYPTION_KEY` | ✅ | 32-byte (base64/AES-GCM) key used to encrypt stored credentials. The API refuses to start without it. |
| `Authentication__Google__ClientId` | ✅ | Google OAuth 2.0 client ID ([Google Cloud Console](https://console.cloud.google.com/)) |
| `Authentication__Google__ClientSecret` | ✅ | Google OAuth 2.0 client secret |
| `Traccar__BaseUrl` | ✅ | Base URL of your Traccar server (e.g. `https://traccar.example.com`) |

> ℹ️ **Traccar and Hologram API keys are managed inside the application** (by an admin user), not via environment variables. They are stored encrypted in the database (AES-GCM) using the key above.

Example:

```bash
export FINDMYCAT_ENCRYPTION_KEY="<your-32-byte-key>"
export Authentication__Google__ClientId="<client-id>.apps.googleusercontent.com"
export Authentication__Google__ClientSecret="<client-secret>"
export Traccar__BaseUrl="https://traccar.example.com"
```

After first sign-in, an admin user can configure the Traccar/Hologram API keys in the app; they are stored encrypted (AES-GCM) in the database.

### 2. Run the API

```bash
dotnet run --project FindMyCat.Api
```

The API listens on `http://localhost:5120` in development. A SQLite database (`findmycat.db`) is created and migrated automatically on startup (except in the `Testing` environment). Swagger UI is available at `/swagger` in development.

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

The Vite dev server proxies `/api`, `/auth` and `/public` requests to `http://localhost:5120`, so the SPA works out of the box at `http://localhost:5173`.

## 🧪 Testing

```bash
# Backend (unit + integration)
dotnet test

# Frontend (unit)
cd frontend && npm test

# Frontend (E2E)
cd frontend && npm run e2e
```

## 🤝 Contributing

Found a bug or want a feature? Open an issue or submit a pull request — the CI workflow will run the backend and frontend test suites on every PR.

## 📚 Related Projects

- [FindMyCat (original project)](https://github.com/FindMyCat) — open-source pet tracker: hardware, firmware, HomeStation, and iOS app
- [Traccar](https://www.traccar.org/) — open-source GPS tracking server
- [Hologram](https://www.hologram.io/) — cellular IoT connectivity

## 📄 License

[MIT](LICENSE)
