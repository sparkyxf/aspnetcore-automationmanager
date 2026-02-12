FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AutomationManager.sln", "."]
COPY ["AutomationManager.Domain/AutomationManager.Domain.csproj", "AutomationManager.Domain/"]
COPY ["AutomationManager.Application/AutomationManager.Application.csproj", "AutomationManager.Application/"]
COPY ["AutomationManager.Infrastructure/AutomationManager.Infrastructure.csproj", "AutomationManager.Infrastructure/"]
COPY ["AutomationManager.Contracts/AutomationManager.Contracts.csproj", "AutomationManager.Contracts/"]
COPY ["AutomationManager.SDK/AutomationManager.SDK.csproj", "AutomationManager.SDK/"]
COPY ["AutomationManager.API/AutomationManager.API.csproj", "AutomationManager.API/"]
RUN dotnet restore "AutomationManager.API/AutomationManager.API.csproj"
COPY . .
WORKDIR "/src/AutomationManager.API"
RUN dotnet build "AutomationManager.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AutomationManager.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutomationManager.API.dll"]