using AutomationManager.Application.Commands;
using AutomationManager.Application.Queries;
using AutomationManager.Contracts;
using MediatR;

namespace AutomationManager.API.Endpoints;

public static class ExecutionEndpoints
{
    public static void MapExecutionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/executions");

        group.MapGet("/", async (IMediator mediator, Guid? agentId, Guid? templateId, int page = 1, int pageSize = 10, string? sortBy = "Id", bool sortDescending = true) =>
        {
            var query = new GetExecutionsQuery(agentId, templateId, page, pageSize, sortBy, sortDescending);
            var result = await mediator.Send(query);
            return Results.Ok(result);
        });

        group.MapPost("/start", async (IMediator mediator, StartExecutionRequest request) =>
        {
            var command = new StartExecutionCommand(new(request.AgentId, request.TemplateId));
            var result = await mediator.Send(command);
            return Results.Ok(result);
        });

        group.MapPost("/{id}/pause", async (IMediator mediator, Guid id) =>
        {
            var command = new PauseExecutionCommand(id);
            await mediator.Send(command);
            return Results.NoContent();
        });

        group.MapPost("/{id}/cancel", async (IMediator mediator, Guid id) =>
        {
            var command = new CancelExecutionCommand(id);
            await mediator.Send(command);
            return Results.NoContent();
        });
    }

    public record StartExecutionRequest(Guid AgentId, Guid TemplateId);
}