using AutomationManager.Application.Queries;
using MediatR;

namespace AutomationManager.API.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/agents");

        group.MapGet("/", async (IMediator mediator, string? search, int page = 1, int pageSize = 10, string? sortBy = "Name", bool sortDescending = false) =>
        {
            var query = new GetAgentsQuery(search, page, pageSize, sortBy, sortDescending);
            var result = await mediator.Send(query);
            return Results.Ok(result);
        });
    }
}