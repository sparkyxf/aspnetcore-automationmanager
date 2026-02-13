using AutomationManager.API.Services;
using AutomationManager.Contracts;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AutomationManager.API.Endpoints;

public static class WebSocketEndpoint
{
    public static void MapWebSocketEndpoint(this WebApplication app)
    {
        app.Map("/ws/agent", async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var agentTracker = context.RequestServices.GetRequiredService<ConnectedAgentTracker>();
            
            logger.LogInformation("WebSocket connection request received");
            await HandleWebSocketAsync(webSocket, agentTracker, logger, context.RequestAborted);
        });
    }

    private static async Task HandleWebSocketAsync(WebSocket webSocket, ConnectedAgentTracker agentTracker, ILogger logger, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4];
        Guid? currentAgentId = null;
        bool isInternalConnection = false;
        
        try
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            while (!result.CloseStatus.HasValue)
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    logger.LogDebug("[WS-IN] Received: {Message}", message);
                    
                    try
                    {
                        using var document = JsonDocument.Parse(message);
                        var root = document.RootElement;

                        // Check if this is a registration message (has Type property)
                        if (root.TryGetProperty("Type", out var typeElement) && typeElement.GetString() == "Register")
                        {
                            if (root.TryGetProperty("AgentId", out var agentIdElement))
                            {
                                currentAgentId = agentIdElement.GetGuid();
                                var agentName = root.TryGetProperty("AgentName", out var nameElement) 
                                    ? nameElement.GetString() ?? $"Agent-{currentAgentId}" 
                                    : $"Agent-{currentAgentId}";
                                
                                // Filter out internal/web connections - they start with "Web-" or "Internal-"
                                isInternalConnection = agentName.StartsWith("Web-") || agentName.StartsWith("Internal-");
                                
                                if (!isInternalConnection)
                                {
                                    agentTracker.RegisterAgent(currentAgentId.Value, agentName, webSocket);
                                    logger.LogInformation("WebSocket connection established for Agent {AgentId} ({AgentName})", 
                                        currentAgentId, agentName);
                                }
                                else
                                {
                                    agentTracker.RegisterWebClient(webSocket);
                                    logger.LogDebug("Internal WebSocket connection established: {AgentName}", agentName);
                                }
                            }
                        }
                        // Check if this is a status message (has Status property, no Type property)
                        else if (currentAgentId.HasValue && !isInternalConnection && root.TryGetProperty("Status", out _))
                        {
                            // Parse as AgentStatusMessage
                            var statusMessage = JsonSerializer.Deserialize<AgentStatusMessage>(message);
                            if (statusMessage != null)
                            {
                                logger.LogInformation("Updating status for Agent {AgentId}: {Status}", currentAgentId, statusMessage.Status);
                                agentTracker.UpdateAgentStatus(currentAgentId.Value, statusMessage);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex, "Failed to parse WebSocket message: {Message}", message);
                    }
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }

            if (!isInternalConnection)
            {
                logger.LogInformation("WebSocket connection closed normally for Agent {AgentId}. Status: {Status}, Description: {Description}", 
                    currentAgentId, result.CloseStatus, result.CloseStatusDescription);
            }
            
            // Close with timeout to prevent hanging
            using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, closeCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout occurred, connection will be aborted in dispose
                logger.LogWarning("WebSocket close timeout for Agent {AgentId}", currentAgentId);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested (e.g., app shutdown)
            if (!isInternalConnection)
            {
                logger.LogInformation("WebSocket connection cancelled during operation for Agent {AgentId}", currentAgentId);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            // This is normal during API shutdown - don't log as warning
            if (!isInternalConnection)
            {
                logger.LogDebug("WebSocket connection closed during shutdown for Agent {AgentId}", currentAgentId);
            }
        }
        catch (WebSocketException ex)
        {
            if (!isInternalConnection)
            {
                logger.LogWarning("WebSocket connection closed unexpectedly for Agent {AgentId}: {Message}", currentAgentId, ex.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in WebSocket connection for Agent {AgentId}", currentAgentId);
        }
        finally
        {
            if (currentAgentId.HasValue && !isInternalConnection)
            {
                agentTracker.UnregisterAgent(currentAgentId.Value);
            }
            else if (isInternalConnection)
            {
                agentTracker.UnregisterWebClient(webSocket);
            }
        }
    }
}