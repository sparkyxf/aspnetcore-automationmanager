using AutomationManager.Contracts;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AutomationManager.SDK;

public class AutomationWebSocketClient : IDisposable
{
    private ClientWebSocket? _webSocket;
    private readonly Uri _serverUri;
    private bool _disposed = false;
    private readonly Action<string>? _logDebug;

    public AutomationWebSocketClient(string serverUrl, Action<string>? logDebug = null)
    {
        _serverUri = new Uri(serverUrl);
        _logDebug = logDebug;
    }

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    /// <summary>
    /// Disposes the current WebSocket connection without marking the client as disposed,
    /// allowing a fresh reconnection attempt with a new ClientWebSocket instance.
    /// </summary>
    public void DisposeConnection()
    {
        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open ||
                    _webSocket.State == WebSocketState.CloseReceived ||
                    _webSocket.State == WebSocketState.CloseSent)
                {
                    _webSocket.Abort();
                }
            }
            catch
            {
                // Ignore errors during abort
            }
            finally
            {
                try { _webSocket.Dispose(); } catch { }
                _webSocket = null;
            }
            _logDebug?.Invoke("[WS] Connection disposed for reconnection");
        }
    }

    public async Task ConnectAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(agentId, $"Agent-{agentId}", cancellationToken);
    }

    public async Task ConnectAsync(Guid agentId, string agentName, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutomationWebSocketClient));

        // Always dispose of any existing connection before creating a new one
        DisposeConnection();

        _webSocket = new ClientWebSocket();
        _logDebug?.Invoke($"[WS] Connecting to {_serverUri}...");
        await _webSocket.ConnectAsync(_serverUri, cancellationToken);
        
        if (_webSocket.State != WebSocketState.Open)
        {
            DisposeConnection();
            throw new InvalidOperationException($"WebSocket connection failed. State: {_webSocket.State}");
        }
        
        _logDebug?.Invoke("[WS] Connection established, sending registration...");

        var registerMessage = new { Type = "Register", AgentId = agentId, AgentName = agentName };
        var messageJson = JsonSerializer.Serialize(registerMessage);
        _logDebug?.Invoke($"[WS-OUT] Sending: {messageJson}");
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken);
        
        _logDebug?.Invoke("[WS] Registration message sent successfully");
    }

    public async Task SendStatusAsync(AgentStatusMessage message, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutomationWebSocketClient));

        if (IsConnected)
        {
            try
            {
                var messageJson = JsonSerializer.Serialize(message);
                _logDebug?.Invoke($"[WS-OUT] Sending: {messageJson}");
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);
                await _webSocket!.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken);
            }
            catch (WebSocketException)
            {
                DisposeConnection();
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }
    }

    public async Task<ExecutionControlMessage?> ReceiveMessageAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutomationWebSocketClient));

        if (IsConnected)
        {
            try
            {
                var buffer = new byte[1024 * 4];
                var result = await _webSocket!.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logDebug?.Invoke($"[WS-IN] Received: {messageJson}");
                    return JsonSerializer.Deserialize<ExecutionControlMessage>(messageJson);
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logDebug?.Invoke("[WS-IN] Close received during ReceiveMessage");
                    DisposeConnection();
                    return null;
                }
            }
            catch (WebSocketException)
            {
                DisposeConnection();
                throw;
            }
        }
        return null;
    }

    public async Task<string?> ReceiveRawMessageAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AutomationWebSocketClient));

        if (IsConnected)
        {
            try
            {
                var buffer = new byte[1024 * 4];
                var result = await _webSocket!.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logDebug?.Invoke("[WS-IN] Connection close received");
                    DisposeConnection();
                    return null;
                }
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logDebug?.Invoke($"[WS-IN] Received: {message}");
                    return message;
                }
            }
            catch (WebSocketException)
            {
                DisposeConnection();
                throw;
            }
        }
        return null;
    }

    public async Task DisconnectAsync()
    {
        await DisconnectAsync(TimeSpan.FromSeconds(5));
    }

    public async Task DisconnectAsync(TimeSpan timeout)
    {
        if (_webSocket is not null && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cts.Token);
            }
            catch (OperationCanceledException)
            {
                _webSocket?.Abort();
            }
            catch (WebSocketException)
            {
                // Connection already closed or failed
            }
        }
        DisposeConnection();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposeConnection();
            _disposed = true;
        }
    }
}