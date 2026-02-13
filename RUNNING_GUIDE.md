# AutomationManager - Quick Start Guide

## ✅ All projects are now runnable!

### Project Status
- ✅ **API**: Runs on `http://localhost:5200`
- ✅ **Web**: Runs on `http://localhost:5192` 
- ✅ **Agent**: Console application that connects to the API

### Running the Projects

#### Option 1: Using VS Code Launch Configurations
Use the VS Code Run and Debug panel (F5) and select:
- **Launch API** - Start the API server only
- **Launch Web** - Start the web UI only  
- **Launch Agent** - Start the console agent only
- **Launch API + Web** - Start both API and Web together
- **Launch All (API + Web + Agent)** - Start all three projects

#### Option 2: Using Terminal Commands
```bash
# Build all projects
dotnet build

# Run API (port 5200)
dotnet run --project AutomationManager.API

# Run Web UI (port 5192) - requires API to be running
dotnet run --project AutomationManager.Web

# Run Agent - requires API to be running
dotnet run --project AutomationManager.Agent
```

#### Option 3: Using Tasks
- Use Ctrl+Shift+P → "Tasks: Run Task" → "build" to build all projects
- Use Ctrl+Shift+P → "Tasks: Run Task" → "watch" to run with file watching

### Configuration
- **Database**: Uses in-memory database for development (no setup required)
- **API URL** (Web/Agent): Configured to connect to `http://localhost:5200`
- **Logging**: Console logging enabled for all projects

### Architecture
```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│    Web UI   │    │  API Server │    │    Agent    │
│  :5192      │◄──►│   :5200     │◄──►│  (Console)  │
└─────────────┘    └─────────────┘    └─────────────┘
```

### Development Notes
- All warnings have been fixed
- Connection strings are configured for development
- Launch configurations support debugging
- Projects can be run independently or together

**Recommended**: Start with "Launch API + Web" compound configuration to get the core functionality running quickly.