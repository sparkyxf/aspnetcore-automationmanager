using AutomationManager.Application.Commands;
using AutomationManager.Application.Queries;
using AutomationManager.Contracts;
using MediatR;

namespace AutomationManager.API.Endpoints;

public static class ScriptTemplateEndpoints
{
    public static void MapScriptTemplateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/script-templates");

        group.MapGet("/", async (IMediator mediator, string? search, int page = 1, int pageSize = 10, string? sortBy = "Name", bool sortDescending = false) =>
        {
            var query = new GetScriptTemplatesQuery(search, page, pageSize, sortBy, sortDescending);
            var result = await mediator.Send(query);
            return Results.Ok(result);
        });

        group.MapPost("/", async (IMediator mediator, ScriptTemplateRequest request) =>
        {
            var command = new CreateScriptTemplateCommand(new(
                request.Name,
                request.Description,
                request.ScriptText,
                Enum.Parse<AutomationManager.Domain.Entities.ExecutionMode>(request.Mode),
                request.LoopCount
            ));
            var result = await mediator.Send(command);
            return Results.Created($"/script-templates/{result.Id}", new ScriptTemplateResponse(
                result.Id,
                result.Name,
                result.Description,
                result.ScriptText,
                result.Mode.ToString(),
                result.LoopCount
            ));
        });

        group.MapPut("/{id}", async (IMediator mediator, Guid id, ScriptTemplateRequest request) =>
        {
            var command = new UpdateScriptTemplateCommand(id, new(
                request.Name,
                request.Description,
                request.ScriptText,
                Enum.Parse<AutomationManager.Domain.Entities.ExecutionMode>(request.Mode),
                request.LoopCount
            ));
            var result = await mediator.Send(command);
            return Results.Ok(new ScriptTemplateResponse(
                result.Id,
                result.Name,
                result.Description,
                result.ScriptText,
                result.Mode.ToString(),
                result.LoopCount
            ));
        });

        group.MapDelete("/{id}", async (IMediator mediator, Guid id) =>
        {
            var command = new DeleteScriptTemplateCommand(id);
            await mediator.Send(command);
            return Results.NoContent();
        });
    }
}