using AutomationManager.API.Services;
using AutomationManager.Application.Queries;
using AutomationManager.Contracts;
using MediatR;

namespace AutomationManager.API.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/agents");

        group.MapGet("/", (ConnectedAgentTracker agentTracker, string? search, int page = 1, int pageSize = 10, string? sortBy = "Name", bool sortDescending = false) =>
        {
            // Get connected agents from in-memory tracker instead of database
            var connectedAgents = agentTracker.GetConnectedAgents();
            
            // Apply search filter
            if (!string.IsNullOrEmpty(search))
            {
                connectedAgents = connectedAgents.Where(a => 
                    a.AgentName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            
            // Apply sorting
            connectedAgents = sortBy switch
            {
                "Name" => sortDescending 
                    ? connectedAgents.OrderByDescending(a => a.AgentName) 
                    : connectedAgents.OrderBy(a => a.AgentName),
                _ => connectedAgents.OrderBy(a => a.AgentName)
            };
            
            var totalCount = connectedAgents.Count();
            
            // Apply pagination
            var items = connectedAgents
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(agent => new AgentResponse(
                    agent.AgentId,
                    agent.AgentName,
                    "Connected", // All agents in tracker are connected
                    agent.LastSeen
                ))
                .ToList();
            
            var response = new PagedResult<AgentResponse>(
                items,
                totalCount,
                page,
                pageSize
            );
            
            return Results.Ok(response);
        });
    }
}