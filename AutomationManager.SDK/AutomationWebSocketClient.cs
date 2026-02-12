using AutomationManager.Contracts;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AutomationManager.SDK;

public class AutomationWebSocketClient : IDisposable
{
    private ClientWebSocket? _webSocket;
    private readonly Uri _serverUri;

    public AutomationWebSocketClient(string serverUrl)
    {
        _serverUri = new Uri(serverUrl);
    }

    public async Task ConnectAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(_serverUri, cancellationToken);

        // Send registration message
        var registerMessage = new { Type = "Register", AgentId = agentId };
        var messageJson = JsonSerializer.Serialize(registerMessage);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task SendStatusAsync(AgentStatusMessage message, CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken);
        }
    }

    public async Task<ExecutionControlMessage?> ReceiveMessageAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            var buffer = new byte[1024 * 4];
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                return JsonSerializer.Deserialize<ExecutionControlMessage>(messageJson);
            }
        }
        return null;
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket is not null)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _webSocket?.Dispose();
    }
}