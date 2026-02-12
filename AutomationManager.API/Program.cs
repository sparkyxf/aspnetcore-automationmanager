using AutomationManager.API.Middleware;
using AutomationManager.API.Endpoints;
using AutomationManager.Application.Handlers;
using AutomationManager.Domain.Entities;
using AutomationManager.Infrastructure;
using MediatR;
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

// Middleware
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseWebSockets();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AutomationDbContext>();
    SeedData.Initialize(context);
}

// Routes
app.MapOpenApi();
app.MapSwagger();
app.UseSwaggerUI();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

// API endpoints
app.MapScriptTemplateEndpoints();
app.MapAgentEndpoints();
app.MapExecutionEndpoints();
app.MapWebSocketEndpoint();

app.Run();
