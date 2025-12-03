using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TRViS.NetworkSyncService;

namespace TRViS.DemoServer.Services;

public class WebSocketHandler
{
    private readonly TimetableService _timetableService;
    private readonly ILogger<WebSocketHandler> _logger;

    public WebSocketHandler(TimetableService timetableService, ILogger<WebSocketHandler> logger)
    {
        _timetableService = timetableService;
        _logger = logger;
    }

    public async Task HandleConnectionAsync(WebSocket webSocket)
    {
        _logger.LogInformation("WebSocket connection established");
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    string message = messageBuilder.ToString();
                    messageBuilder.Clear();
                    
                    _logger.LogDebug("Received message: {Message}", message);
                    
                    var response = await ProcessMessageAsync(message);
                    if (response != null)
                    {
                        await SendMessageAsync(webSocket, response);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket handler");
        }
        finally
        {
            _logger.LogInformation("WebSocket connection closed");
        }
    }

    private async Task<string?> ProcessMessageAsync(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!root.TryGetProperty("MessageType", out var messageTypeElement))
            {
                return null;
            }

            string? messageType = messageTypeElement.GetString();

            return messageType switch
            {
                "GetFeatures" => ProcessGetFeatures(),
                "SearchTrain" => ProcessSearchTrain(root),
                "GetTrainData" => ProcessGetTrainData(root),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return null;
        }
    }

    private string ProcessGetFeatures()
    {
        var features = new List<string> { "SyncedData", "Timetable" };
        
        if (_timetableService.IsTrainSearchEnabled)
        {
            features.Add("TrainSearch");
        }

        var response = new FeaturesResponse
        {
            Features = features.ToArray()
        };

        return JsonSerializer.Serialize(response);
    }

    private string ProcessSearchTrain(JsonElement root)
    {
        string requestId = root.GetProperty("RequestId").GetString() ?? "";
        string trainNumber = root.GetProperty("TrainNumber").GetString() ?? "";

        var searchResponse = _timetableService.SearchTrains(trainNumber);
        searchResponse.RequestId = requestId;

        return JsonSerializer.Serialize(searchResponse);
    }

    private string ProcessGetTrainData(JsonElement root)
    {
        string requestId = root.GetProperty("RequestId").GetString() ?? "";
        string trainId = root.GetProperty("TrainId").GetString() ?? "";

        var dataResponse = _timetableService.GetTrainData(trainId);
        dataResponse.RequestId = requestId;

        return JsonSerializer.Serialize(dataResponse);
    }

    private async Task SendMessageAsync(WebSocket webSocket, string message)
    {
        if (webSocket.State != WebSocketState.Open)
            return;

        var bytes = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None
        );
        
        _logger.LogDebug("Sent message: {Message}", message);
    }
}
