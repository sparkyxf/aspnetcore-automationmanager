using AutomationManager.API.Services;
using AutomationManager.Application.Commands;
using AutomationManager.Application.Queries;
using AutomationManager.Contracts;
using AutomationManager.Domain.Entities;
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
            
            var response = new PagedResult<ScriptExecutionResponse>(
                result.Items.Select(dto => new ScriptExecutionResponse(
                    dto.Id,
                    dto.ScriptTemplateId, // Map ScriptTemplateId to TemplateId
                    dto.Status.ToString(), // Convert enum to string
                    DateTimeOffset.UtcNow, // Placeholder for StartTime - needs proper implementation
                    dto.Status == ExecutionStatus.Completed || dto.Status == ExecutionStatus.Canceled 
                        ? DateTimeOffset.UtcNow : null, // Placeholder for EndTime
                    dto.CurrentCommand, // Map CurrentCommand to Output as placeholder
                    null // Placeholder for ErrorOutput
                )),
                result.TotalCount,
                result.Page,
                result.PageSize
            );
            
            return Results.Ok(response);
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

        // Script execution control endpoints for direct agent communication
        group.MapPost("/script-command", async (ConnectedAgentTracker agentTracker, ScriptExecutionCommand request) =>
        {
            var executionControl = new ExecutionControlMessage(
                SetMode: request.SetMode,
                ScriptText: request.ScriptText,
                Mode: request.Mode,
                LoopCount: request.LoopCount
            );

            var success = await agentTracker.SendExecutionControlToAgentAsync(
                request.AgentId, 
                executionControl);
                
            return success ? Results.Ok(new { Success = true, Message = $"Script command '{request.SetMode}' sent successfully" }) 
                          : Results.BadRequest(new { Success = false, Message = "Failed to send script command to agent" });
        });

        // Legacy and convenience endpoints
        group.MapPost("/run-on-agent", async (ConnectedAgentTracker agentTracker, RunOnAgentRequest request) =>
        {
            var executionControl = new ExecutionControlMessage(
                SetMode: "run",
                ScriptText: request.ScriptText,
                Mode: request.Mode,
                LoopCount: request.LoopCount
            );

            var success = await agentTracker.SendExecutionControlToAgentAsync(
                request.AgentId, 
                executionControl);
                
            return success ? Results.Ok(new { Success = true, Message = "Script execution started" }) 
                          : Results.BadRequest(new { Success = false, Message = "Failed to send script to agent" });
        });

        group.MapPost("/pause-agent/{agentId}", async (ConnectedAgentTracker agentTracker, Guid agentId) =>
        {
            var executionControl = new ExecutionControlMessage("pause");
            var success = await agentTracker.SendExecutionControlToAgentAsync(agentId, executionControl);
            return success ? Results.Ok(new { Success = true, Message = "Execution paused" }) 
                          : Results.BadRequest(new { Success = false, Message = "Failed to pause execution" });
        });

        group.MapPost("/resume-agent/{agentId}", async (ConnectedAgentTracker agentTracker, Guid agentId) =>
        {
            var executionControl = new ExecutionControlMessage("resume");
            var success = await agentTracker.SendExecutionControlToAgentAsync(agentId, executionControl);
            return success ? Results.Ok(new { Success = true, Message = "Execution resumed" }) 
                          : Results.BadRequest(new { Success = false, Message = "Failed to resume execution" });
        });

        group.MapPost("/stop-agent/{agentId}", async (ConnectedAgentTracker agentTracker, Guid agentId) =>
        {
            var executionControl = new ExecutionControlMessage("stop");
            var success = await agentTracker.SendExecutionControlToAgentAsync(agentId, executionControl);
            return success ? Results.Ok(new { Success = true, Message = "Execution stopped" }) 
                          : Results.BadRequest(new { Success = false, Message = "Failed to stop execution" });
        });
    }

    public record StartExecutionRequest(Guid AgentId, Guid TemplateId);
    public record RunOnAgentRequest(
        Guid AgentId, 
        string ScriptText, 
        AutomationManager.Contracts.ExecutionMode Mode = AutomationManager.Contracts.ExecutionMode.RunOnce, 
        int? LoopCount = null
    );
}