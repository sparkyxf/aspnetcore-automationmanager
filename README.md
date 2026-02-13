# AutomationManager

A production-grade solution for managing automation scripts executed by remote agents, built with Clean Architecture using .NET 9.

## Architecture

The solution follows Clean Architecture with clear separation of concerns:

| Project | Description |
|---|---|
| **AutomationManager.API** | ASP.NET Core Minimal API — REST endpoints, WebSocket hub, Swagger/OpenAPI, Serilog structured logging, health checks |
| **AutomationManager.Web** | Blazor Server UI — real-time dashboard for agents, script templates, and execution control |
| **AutomationManager.Agent** | Windows background service with system tray icon — connects to the API via WebSocket to receive and execute scripts |
| **AutomationManager.Domain** | Core domain: entities (`Agent`, `ScriptTemplate`, `ScriptExecution`, `ScriptCommand`, `ScriptCommandGroup`, `ExecutionLog`), domain services (`ScriptParser`, `ScriptValidator`, `ExecutionEngine`), validators |
| **AutomationManager.Application** | CQRS layer — commands, queries, handlers, DTOs, interfaces (uses MediatR + FluentValidation) |
| **AutomationManager.Infrastructure** | EF Core `DbContext`, Repository/UnitOfWork, design-time factory, migrations, seed data, dependency injection wiring |
| **AutomationManager.Contracts** | Shared request/response DTOs and WebSocket message contracts consumed by API, Web, and SDK |
| **AutomationManager.SDK** | `AutomationApiClient` (HTTP) and `AutomationWebSocketClient` (WS) — reusable typed clients for agent and Web projects |

## Features

- Script template CRUD with a custom scripting language (key presses, mouse actions, delays, command groups, loops)
- Real-time agent monitoring — agents register over WebSocket; state tracked in memory
- Script execution control: run, pause, resume, stop — sent to agents over WebSocket
- Execution history with logs
- Structured logging via Serilog (console sink, configurable in `appsettings.json`)
- Health checks: `/health/live` (liveness) and `/health/ready` (readiness, includes DB check)
- Swagger UI available in Development mode
- Graceful shutdown with WebSocket close handshake on both API and Web
- Horizontal scaling ready (stateless API)

## API Endpoints

### Script Templates — `/script-templates`

| Method | Path | Description |
|---|---|---|
| `GET` | `/script-templates` | List templates (paged, searchable, sortable) |
| `GET` | `/script-templates/{id}` | Get template by ID |
| `POST` | `/script-templates` | Create template |
| `PUT` | `/script-templates/{id}` | Update template |
| `DELETE` | `/script-templates/{id}` | Delete template |

### Agents — `/agents`

| Method | Path | Description |
|---|---|---|
| `GET` | `/agents` | List connected agents (paged, searchable) |

### Executions — `/executions`

| Method | Path | Description |
|---|---|---|
| `GET` | `/executions` | List executions (filterable by agent/template) |
| `POST` | `/executions/start` | Start a new execution |
| `POST` | `/executions/{id}/pause` | Pause execution |
| `POST` | `/executions/{id}/cancel` | Cancel execution |
| `POST` | `/executions/script-command` | Send script command directly to an agent |
| `POST` | `/executions/run-on-agent` | Run script text on a specific agent |
| `POST` | `/executions/pause-agent/{agentId}` | Pause execution on agent |
| `POST` | `/executions/resume-agent/{agentId}` | Resume execution on agent |
| `POST` | `/executions/stop-agent/{agentId}` | Stop execution on agent |

### WebSocket — `/ws/agent`

Agents connect here and send a `Register` message with their `AgentId` and `AgentName`. The API tracks connections and forwards execution commands over the socket.

### Health Checks

| Path | Purpose |
|---|---|
| `/health/live` | Liveness probe (always healthy) |
| `/health/ready` | Readiness probe (includes database connectivity check) |

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) — `dotnet tool install --global dotnet-ef`
- PostgreSQL 15+ (optional — the app defaults to an in-memory database for local development)
- Docker & Docker Compose (optional — for containerized deployment)

### Build

```bash
dotnet build
```

### Run Locally (InMemory database — no setup needed)

Start the API and Web projects in separate terminals:

```bash
# Terminal 1 — API (listens on http://localhost:5200)
dotnet run --project AutomationManager.API

# Terminal 2 — Blazor Web UI
dotnet run --project AutomationManager.Web

# Terminal 3 — Agent (Windows only, connects to API via WebSocket)
dotnet run --project AutomationManager.Agent
```

Swagger UI is available at `http://localhost:5200/swagger` when running in Development mode.

### Run with Docker Compose (PostgreSQL)

```bash
docker-compose up --build
```

This starts three containers:

| Service | Container Port | Host Port | Notes |
|---|---|---|---|
| `api` | 8080 | **5200** | API with PostgreSQL connection |
| `web` | 8080 | **5201** | Blazor UI, connects to API internally |
| `db` | 5432 | **5432** | PostgreSQL 15, data persisted in `postgres_data` volume |

The connection string is injected via environment variable:
```
Host=db;Database=AutomationManager;Username=postgres;Password=password
```

### Publish AutomationManager.Agent

```bash
dotnet publish -c Release -r win-x64 `
   --self-contained true `
   -p:PublishSingleFile=true `
   -p:TrimMode=link `
   -p:EnableCompressionInSingleFile=true
```

## Configuration

### API — `AutomationManager.API/appsettings.json`

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `InMemory` | Set to a PostgreSQL connection string for production (`Host=...;Database=...;Username=...;Password=...`) |
| `Kestrel:Endpoints:Http:Url` | `http://*:5200` | API listen address |
| `Serilog` | Console sink | Structured logging configuration |

### Web — `AutomationManager.Web/appsettings.json`

| Key | Default | Description |
|---|---|---|
| `ApiBaseUrl` | `http://localhost:5200` | URL of the API the Web UI connects to |

### Agent — `AutomationManager.Agent/appsettings.json`

| Key | Default | Description |
|---|---|---|
| `ApiBaseUrl` | `http://localhost:5200` | URL of the API the agent connects to via WebSocket |
| `AgentName` | Machine name | Display name for the agent |
| `AgentId` | Auto-generated GUID | Persistent agent identifier |

## Database & Migrations

### How It Works

- The `DependencyInjection.AddInfrastructure()` method configures EF Core:
  - If the connection string is `InMemory` or empty → uses `UseInMemoryDatabase` (no migrations needed)
  - If a real connection string is provided → uses `UseNpgsql` with PostgreSQL
- On API startup, pending migrations are **automatically applied** when using PostgreSQL. For InMemory, `EnsureCreated()` is called instead.
- A `DesignTimeDbContextFactory` in the Infrastructure project enables `dotnet ef` CLI commands.

### Creating a New Migration

After changing entities or `DbContext` configuration:

```bash
dotnet ef migrations add <MigrationName> \
  --project AutomationManager.Infrastructure \
  --startup-project AutomationManager.API \
  --output-dir Migrations
```

> **Note:** Migrations target PostgreSQL. The design-time factory defaults to `Host=localhost;Database=AutomationManager;Username=postgres;Password=password` when no real connection string is configured.

### Applying Migrations Manually

```bash
dotnet ef database update \
  --project AutomationManager.Infrastructure \
  --startup-project AutomationManager.API
```

### Reverting a Migration

```bash
dotnet ef migrations remove \
  --project AutomationManager.Infrastructure \
  --startup-project AutomationManager.API
```

### Seed Data

`SeedData.Initialize()` runs on every startup and inserts sample agents and a sample script template if the `Agents` table is empty.

## Middleware

| Middleware | Purpose |
|---|---|
| `CorrelationIdMiddleware` | Attaches a correlation ID to each request for distributed tracing |
| `GlobalExceptionMiddleware` | Catches unhandled exceptions and returns structured `ProblemDetails` responses |

## Testing

```bash
dotnet test
```

Tests are in `AutomationManager.Tests` and include:
- `ExecutionEngineTests` — script execution logic
- `ScriptParserTests` — custom script language parsing
- `ScriptValidatorTests` — script validation rules

## Key Technology Stack

| Area | Technology |
|---|---|
| Framework | .NET 9 |
| API | ASP.NET Core Minimal APIs |
| UI | Blazor Server |
| ORM | Entity Framework Core 9 |
| Database | PostgreSQL 15 / InMemory |
| CQRS | MediatR |
| Validation | FluentValidation |
| Logging | Serilog |
| Real-time | WebSockets (native ASP.NET Core) |
| Containerization | Docker, Docker Compose |

## Deployment

The API is stateless and suitable for horizontal scaling behind a load balancer or in Kubernetes.

- Use `/health/live` as the **liveness probe**
- Use `/health/ready` as the **readiness probe** (verifies database connectivity)
- Migrations are applied automatically on startup — safe for rolling deployments
- WebSocket connections are per-instance; consider sticky sessions or a distributed backplane for multi-instance WebSocket scenarios