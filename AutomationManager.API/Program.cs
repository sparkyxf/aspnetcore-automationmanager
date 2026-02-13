using AutomationManager.API.Middleware;
using AutomationManager.API.Endpoints;
using AutomationManager.API.Services;
using AutomationManager.Application.Handlers;
using AutomationManager.Domain.Entities;
using AutomationManager.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.IdentityModel.Tokens;
// using System.Text;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((context, config) =>
{
    config.WriteTo.Console();
    config.ReadFrom.Configuration(context.Configuration);
});

// Services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AutomationDbContext>("database");

// Add in-memory agent tracker
builder.Services.AddSingleton<ConnectedAgentTracker>();

// WebSocket support is built into ASP.NET Core, no need to add services for it

// Commented JWT setup
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer = true,
//             ValidateAudience = true,
//             ValidateLifetime = true,
//             ValidateIssuerSigningKey = true,
//             ValidIssuer = builder.Configuration["Jwt:Issuer"],
//             ValidAudience = builder.Configuration["Jwt:Audience"],
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
//         };
//     });
// builder.Services.AddAuthorization();

var app = builder.Build();

// Register graceful WebSocket shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var agentTracker = app.Services.GetRequiredService<ConnectedAgentTracker>();
    agentTracker.CloseAllConnectionsAsync().GetAwaiter().GetResult();
});

// Middleware
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseWebSockets();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AutomationDbContext>();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // Apply pending migrations when using a real database (not InMemory)
    if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("InMemory"))
    {
        context.Database.Migrate();
    }
    else
    {
        context.Database.EnsureCreated();
    }

    SeedData.Initialize(context);
}

// Routes
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

// API endpoints
app.MapScriptTemplateEndpoints();
app.MapAgentEndpoints();
app.MapExecutionEndpoints();
app.MapWebSocketEndpoint();

app.Run();
