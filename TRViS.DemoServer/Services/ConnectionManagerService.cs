using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace TRViS.DemoServer.Services;

public class ConnectionManagerService
{
    private readonly ConcurrentDictionary<string, ClientConnection> _connections = new();
    private int _nextId = 1;

    public event EventHandler<ConnectionEventArgs>? ConnectionAdded;
    public event EventHandler<ConnectionEventArgs>? ConnectionRemoved;

    public string AddConnection(WebSocket webSocket, string ipAddress)
    {
        var id = $"client_{_nextId++}";
        var connection = new ClientConnection
        {
            Id = id,
            WebSocket = webSocket,
            IpAddress = ipAddress,
            ConnectedAt = DateTime.Now
        };

        _connections.TryAdd(id, connection);
        ConnectionAdded?.Invoke(this, new ConnectionEventArgs { Connection = connection });
        return id;
    }

    public void RemoveConnection(string id)
    {
        if (_connections.TryRemove(id, out var connection))
        {
            connection.DisconnectedAt = DateTime.Now;
            ConnectionRemoved?.Invoke(this, new ConnectionEventArgs { Connection = connection });
        }
    }

    public IEnumerable<ClientConnection> GetAllConnections()
    {
        return _connections.Values.ToList();
    }

    public ClientConnection? GetConnection(string id)
    {
        _connections.TryGetValue(id, out var connection);
        return connection;
    }

    public int ActiveConnectionCount => _connections.Count;

    public async Task BroadcastMessageAsync(string message)
    {
        var tasks = new List<Task>();
        foreach (var connection in _connections.Values)
        {
            if (connection.WebSocket.State == WebSocketState.Open)
            {
                tasks.Add(SendMessageAsync(connection.WebSocket, message));
            }
        }
        await Task.WhenAll(tasks);
    }

    private async Task SendMessageAsync(WebSocket webSocket, string message)
    {
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None
            );
        }
        catch
        {
            // Ignore send failures
        }
    }
}

public class ClientConnection
{
    public string Id { get; set; } = string.Empty;
    public WebSocket WebSocket { get; set; } = null!;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }
    public string? SelectedTrainId { get; set; }
    public double LocationMeters { get; set; }
    public bool CanStart { get; set; }
}

public class ConnectionEventArgs : EventArgs
{
    public ClientConnection Connection { get; set; } = null!;
}
