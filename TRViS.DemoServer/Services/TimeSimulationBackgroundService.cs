using System.Text.Json;

namespace TRViS.DemoServer.Services;

public class TimeSimulationBackgroundService : BackgroundService
{
    private readonly TimeSimulationService _timeService;
    private readonly ConnectionManagerService _connectionManager;
    private readonly ILogger<TimeSimulationBackgroundService> _logger;

    public TimeSimulationBackgroundService(
        TimeSimulationService timeService,
        ConnectionManagerService connectionManager,
        ILogger<TimeSimulationBackgroundService> logger)
    {
        _timeService = timeService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Time Simulation Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _timeService.Tick();

                // Broadcast SyncedData to all connected clients
                if (_connectionManager.ActiveConnectionCount > 0)
                {
                    await BroadcastSyncedDataAsync();
                }

                // Update every 100ms
                await Task.Delay(100, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in time simulation background service");
            }
        }

        _logger.LogInformation("Time Simulation Background Service stopped");
    }

    private async Task BroadcastSyncedDataAsync()
    {
        try
        {
            var connections = _connectionManager.GetAllConnections();
            var currentTime = _timeService.CurrentTimeMilliseconds;
            
            foreach (var connection in connections)
            {
                try
                {
                    if (connection.WebSocket.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        var syncedData = new
                        {
                            MessageType = "SyncedData",
                            Time_ms = currentTime,
                            Location_m = connection.LocationMeters,
                            CanStart = connection.CanStart
                        };

                        var json = JsonSerializer.Serialize(syncedData);
                        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                        await connection.WebSocket.SendAsync(
                            new ArraySegment<byte>(bytes),
                            System.Net.WebSockets.WebSocketMessageType.Text,
                            endOfMessage: true,
                            cts.Token
                        );
                    }
                }
                catch (OperationCanceledException)
                {
                    // Send timeout or cancelled, skip this connection
                }
                catch (System.Net.WebSockets.WebSocketException)
                {
                    // WebSocket error, connection will be cleaned up elsewhere
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting synced data");
        }
    }
}
