FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AutomationManager.sln", "."]
COPY ["AutomationManager.Domain/AutomationManager.Domain.csproj", "AutomationManager.Domain/"]
COPY ["AutomationManager.Application/AutomationManager.Application.csproj", "AutomationManager.Application/"]
COPY ["AutomationManager.Infrastructure/AutomationManager.Infrastructure.csproj", "AutomationManager.Infrastructure/"]
COPY ["AutomationManager.Contracts/AutomationManager.Contracts.csproj", "AutomationManager.Contracts/"]
COPY ["AutomationManager.SDK/AutomationManager.SDK.csproj", "AutomationManager.SDK/"]
COPY ["AutomationManager.Web/AutomationManager.Web.csproj", "AutomationManager.Web/"]
RUN dotnet restore "AutomationManager.Web/AutomationManager.Web.csproj"
COPY . .
WORKDIR "/src/AutomationManager.Web"
RUN dotnet build "AutomationManager.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AutomationManager.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutomationManager.Web.dll"]