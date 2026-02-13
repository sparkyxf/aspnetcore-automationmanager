using Microsoft.AspNetCore.Diagnostics;
using System.Net;

namespace AutomationManager.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");

            // Don't try to modify the response if it has already started (e.g., WebSocket connections)
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("The response has already started, cannot send error details");
                throw;
            }

            var problemDetails = new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title = "An error occurred",
                status = (int)HttpStatusCode.InternalServerError,
                detail = context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true
                    ? ex.Message
                    : "Internal server error",
                instance = context.Request.Path
            };

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}