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
            await HandleWebSocketAsync(webSocket, context.RequestServices);
        });
    }

    private static async Task HandleWebSocketAsync(WebSocket webSocket, IServiceProvider services)
    {
        var buffer = new byte[1024 * 4];
        var connectedAgents = new Dictionary<Guid, WebSocket>();

        WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!result.CloseStatus.HasValue)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var baseMessage = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message);

            if (baseMessage is not null && baseMessage.TryGetValue("Type", out var typeElement))
            {
                var type = typeElement.GetString();
                if (type == "Register" && baseMessage.TryGetValue("AgentId", out var agentIdElement))
                {
                    var agentId = agentIdElement.GetGuid();
                    connectedAgents[agentId] = webSocket;
                    // Log registration
                }
                else if (type == "Status" && baseMessage.TryGetValue("AgentId", out agentIdElement))
                {
                    var agentId = agentIdElement.GetGuid();
                    // Handle status update
                }
            }

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }
}