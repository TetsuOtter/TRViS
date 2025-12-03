# TRViS Demo Server Implementation Guide

## Overview
This document provides guidance for implementing a Blazor Server application to test and demonstrate the TRViS train search functionality.

## Completed Features in TRViS Client

### 1. Train Search ✅
- Search trains by train number via WebSocket
- Display search results with full train metadata
- Confirmation dialog before displaying train
- State management for returning to scheduled train

### 2. Automatic Feature Detection ✅
- Calls `GetFeatures()` automatically on WebSocket connection
- Exposes `ServerFeatures` property
- Provides `IsFeatureSupported(string)` method

### 3. Search History ✅
- Tracks last 10 train number searches
- Accessible via `GetSearchHistory()` method

### 4. Hako Tab Visibility Control ✅
- Automatically hides "Hako" tab when displaying searched trains
- Shows tab when returning to scheduled train

## Demo Server Requirements

### Core Services Needed

#### 1. TimetableService
```csharp
public class TimetableService
{
    // Store train data in memory
    // Provide search functionality
    // Support add/remove/edit operations
    public bool IsTrainSearchEnabled { get; set; }
    public TrainSearchResponse SearchTrains(string trainNumber);
    public TrainDataResponse GetTrainData(string trainId);
}
```

#### 2. WebSocketHandler
```csharp
public class WebSocketHandler
{
    // Handle WebSocket connections
    // Process incoming messages
    // Send responses
    // Broadcast updates to all connected clients
    public async Task HandleConnectionAsync(WebSocket webSocket);
}
```

#### 3. TimeSimulationService
```csharp
public class TimeSimulationService
{
    public enum TimeSpeed { Normal = 1, Fast30 = 30, Fast60 = 60 }
    public TimeSpeed CurrentSpeed { get; set; }
    public long CurrentTimeMs { get; }
    // Broadcast time updates to WebSocket clients
}
```

### Blazor Components Needed

#### 1. TrainManagement.razor
- List all trains
- Add/Edit/Delete trains
- Toggle train search feature on/off

#### 2. ConnectionStatus.razor
- Show connected TRViS clients
- Display connection status

#### 3. TimeControl.razor
- Select time speed (1x, 30x, 60x)
- Display current simulated time
- Start/Stop simulation

#### 4. QRCodeDisplay.razor
- Generate QR code with AppLink URL
- Format: `trvis://connect?url=ws://server:port/ws`

### WebSocket Protocol Implementation

The server must handle these message types:

#### 1. GetFeatures (Client → Server)
```json
{ "MessageType": "GetFeatures" }
```
Response:
```json
{
  "MessageType": "Features",
  "Features": ["TrainSearch", "SyncedData", "Timetable"]
}
```

#### 2. SearchTrain (Client → Server)
```json
{
  "MessageType": "SearchTrain",
  "TrainNumber": "1234",
  "RequestId": "unique-id"
}
```
Response: See `TrainSearchResponse` in `TrainSearchModels.cs`

#### 3. GetTrainData (Client → Server)
```json
{
  "MessageType": "GetTrainData",
  "TrainId": "train_123",
  "RequestId": "unique-id"
}
```
Response: See `TrainDataResponse` in `TrainSearchModels.cs`

#### 4. SyncedData (Server → Client, Broadcast)
```json
{
  "MessageType": "SyncedData",
  "Location_m": 1234.5,
  "Time_ms": 3600000,
  "CanStart": true
}
```

### Sample Data Structure

```csharp
public class SampleTrain
{
    public string TrainId { get; set; }
    public string TrainNumber { get; set; }
    public string WorkId { get; set; }
    public string WorkName { get; set; }
    public string WorkGroupId { get; set; }
    public string StartStation { get; set; }
    public string EndStation { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public int Direction { get; set; }
    public string Destination { get; set; }
}
```

### Program.cs Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<TimetableService>();
builder.Services.AddSingleton<TimeSimulationService>();
builder.Services.AddHostedService<TimeSimulationBackgroundService>();

var app = builder.Build();

// Configure WebSocket middleware
app.UseWebSockets();
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handler = new WebSocketHandler(
            context.RequestServices.GetRequiredService<TimetableService>(),
            context.RequestServices.GetRequiredService<TimeSimulationService>()
        );
        await handler.HandleConnectionAsync(webSocket);
    }
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

### Testing Scenarios

1. **Feature Detection Test**
   - Disable train search in UI
   - Connect from TRViS
   - Verify GetFeatures returns appropriate features
   - Verify train search is disabled in TRViS

2. **Train Search Test**
   - Add sample trains with various train numbers
   - Search from TRViS
   - Verify results display correctly
   - Select train and verify timetable displays

3. **Timeout Test**
   - Add artificial delay in server response
   - Verify TRViS shows timeout message

4. **Multiple Connections Test**
   - Connect multiple TRViS instances
   - Verify time/location broadcasts to all clients
   - Verify searches work independently

5. **Search History Test**
   - Perform multiple searches
   - Verify history is maintained
   - Verify duplicates are removed

6. **Hako Tab Test**
   - Display a scheduled train (Hako tab visible)
   - Search and display a different train
   - Verify Hako tab is hidden
   - Return to scheduled train
   - Verify Hako tab is visible again

## Next Steps

1. Implement WebSocketHandler with message routing
2. Create Blazor UI components for management
3. Add QR code generation (use QRCoder NuGet package)
4. Implement time simulation with configurable speed
5. Add HTTP endpoints for alternative data delivery
6. Test with actual TRViS client

## References

- Protocol Specification: `/docs/md/WebSocketProtocol.md`
- Models: `TRViS.NetworkSyncService/TrainSearchModels.cs`
- Client Implementation: `TRViS/DTAC/PageParts/QuickSwitchPopup.xaml.cs`
