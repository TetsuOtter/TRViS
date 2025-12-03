# TRViS Demo Server

A Blazor Server application for testing and demonstrating the TRViS train search functionality.

## ✅ Implemented Features

- **WebSocket Server**: Fully functional WebSocket endpoint at `/ws`
- **Train Search**: Search trains by train number via `SearchTrain` messages
- **Train Data Retrieval**: Get complete train timetable via `GetTrainData` messages
- **Feature Discovery**: Responds to `GetFeatures` requests
- **Train Search Toggle**: Enable/disable train search feature for testing (useful for testing feature detection)
- **Sample Data**: Pre-loaded with 3 sample trains for testing
- **Web UI**: Simple management interface showing connection info and train list

## Sample Trains

The server comes with these sample trains for testing:

| Train Number | Work Name | Route | Time |
|--------------|-----------|-------|------|
| 1234 | 行路1 | 東京 → 大阪 | 09:00 - 12:30 |
| 5678 | 行路1 | 大阪 → 東京 | 14:00 - 17:30 |
| 9999 | 行路2 | 名古屋 → 京都 | 10:30 - 11:45 |

## Running the Server

```bash
cd TRViS.DemoServer
dotnet run
```

The server will start on:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

Navigate to either URL in your browser to see the management interface.

## Connecting from TRViS

Use one of these WebSocket URLs to connect TRViS:
- **Development (insecure)**: `ws://localhost:5000/ws`
- **Production (secure)**: `wss://localhost:5001/ws`

## Testing the Train Search Feature

1. **Start the demo server**:
   ```bash
   cd TRViS.DemoServer
   dotnet run
   ```

2. **Open TRViS** and connect to WebSocket URL: `ws://localhost:5000/ws`

3. **Test Feature Detection**:
   - TRViS should automatically call `GetFeatures` on connection
   - Server responds with `["SyncedData", "Timetable", "TrainSearch"]`
   - Toggle "Train Search Enabled" in web UI to test feature detection

4. **Test Train Search**:
   - In TRViS, open QuickSwitchPopup
   - Go to "Search" tab
   - Enter train number: `1234`
   - Click "検索" button
   - Should see result: "1234 - 行路1 (東京 → 大阪)"

5. **Test Train Selection**:
   - Click on search result
   - Confirm in dialog
   - Train timetable should be displayed in TRViS
   - "Hako" tab should be hidden

6. **Test Return to Scheduled Train**:
   - Click "所定列車に戻る" button
   - Should return to original train
   - "Hako" tab should reappear

7. **Test Search History**:
   - Search for different trains (5678, 9999)
   - Search history tracks last 10 searches

8. **Test Timeout**:
   - Disconnect server while TRViS is running
   - Try to search
   - Should show timeout error after 10 seconds

## Protocol Implementation

The server implements the protocol specified in `../docs/md/WebSocketProtocol.md`:

### Supported Messages

#### 1. GetFeatures (Client → Server)
```json
{ "MessageType": "GetFeatures" }
```
Response includes `"TrainSearch"` if feature is enabled in web UI.

#### 2. SearchTrain (Client → Server)
```json
{
  "MessageType": "SearchTrain",
  "TrainNumber": "1234",
  "RequestId": "unique-id"
}
```
Returns matching trains from sample data.

#### 3. GetTrainData (Client → Server)
```json
{
  "MessageType": "GetTrainData",
  "TrainId": "train_001",
  "RequestId": "unique-id"
}
```
Returns complete train timetable data.

## Architecture

```
TRViS.DemoServer/
├── Services/
│   ├── TimetableService.cs     # Train data management
│   └── WebSocketHandler.cs     # WebSocket message processing
├── Components/
│   └── Pages/
│       └── Home.razor           # Management UI
└── Program.cs                   # App configuration
```

### WebSocketHandler
Handles incoming WebSocket connections and processes messages:
- Parses JSON messages
- Routes to appropriate handler methods
- Sends JSON responses back to client
- Logs all activity

### TimetableService
Manages train data:
- Stores sample trains in memory
- Provides search functionality
- Controls train search feature flag
- Returns train data in protocol format

## Future Enhancements

The following features from the implementation guide are not yet implemented:

- ⏳ Time simulation service (1x, 30x, 60x speeds)
- ⏳ Location simulation with `SyncedData` broadcasts
- ⏳ QR code generation for AppLink
- ⏳ HTTP endpoints (REST API)
- ⏳ Multi-client connection tracking
- ⏳ Real-time timetable editing
- ⏳ Auto-sync changes to connected clients

These can be added in future iterations based on testing needs.

## Development

To add more trains, edit `TimetableService.InitializeSampleData()` in `Services/TimetableService.cs`.

To modify protocol handling, edit `WebSocketHandler.ProcessMessageAsync()` in `Services/WebSocketHandler.cs`.
