# AutomationManager

A production-grade solution for managing automation scripts executed by remote agents, built with Clean Architecture using .NET 9.

## Architecture

- **AutomationManager.API**: Minimal APIs with Swagger, WebSocket support
- **AutomationManager.Web**: Blazor Server UI
- **AutomationManager.Agent**: Windows background service with tray icon
- **AutomationManager.Domain**: Core business logic and entities
- **AutomationManager.Application**: Use cases and CQRS with MediatR
- **AutomationManager.Infrastructure**: EF Core, repositories, external services
- **AutomationManager.Contracts**: Shared DTOs and contracts
- **AutomationManager.SDK**: Reusable API and WebSocket clients

## Features

- Script template management with custom scripting language
- Real-time agent monitoring and script execution
- WebSocket communication for live updates
- Horizontal scaling ready (stateless)
- Health checks and structured logging

## Getting Started

### Prerequisites

- .NET 9 SDK
- PostgreSQL (optional, defaults to InMemory)

### Build and Run

```bash
dotnet build
dotnet run --project AutomationManager.API
dotnet run --project AutomationManager.Web
dotnet run --project AutomationManager.Agent
```

### Docker

```bash
docker-compose up --build
```

## CLI Commands Used

```bash
dotnet new sln -n AutomationManager
dotnet new classlib -n AutomationManager.Domain --framework net9.0
# ... (all project creations)
dotnet sln add *.csproj
dotnet add reference ...
dotnet add package ...
```

## Database

Default: InMemory
Production: PostgreSQL (update connection string in appsettings.json)

Migrations: `dotnet ef migrations add Initial --project AutomationManager.Infrastructure`

## Testing

```bash
dotnet test
```

## Deployment

Stateless design supports Kubernetes deployment. Use health checks for readiness/liveness probes.