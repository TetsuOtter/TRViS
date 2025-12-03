# TRViS Demo Server

A fully-featured Blazor Server application for testing and demonstrating the TRViS train search functionality.

## ✅ Implemented Features

### Core WebSocket Features
- **WebSocket Server**: Fully functional WebSocket endpoint at `/ws`
- **Train Search**: Search trains by train number via `SearchTrain` messages
- **Train Data Retrieval**: Get complete train timetable via `GetTrainData` messages
- **Feature Discovery**: Responds to `GetFeatures` requests
- **Train Search Toggle**: Enable/disable train search feature for testing

### Advanced Features
- **Time Simulation**: Configurable time speeds (1x, 30x, 60x)
  - Start/Stop/Reset controls
  - Real-time time advancement
  - Synchronized across all clients
  
- **Position Simulation**: Per-client location and status management
  - Adjustable location (meters) for each client
  - CanStart flag per client
  - Real-time SyncedData broadcasting every 100ms
  
- **Multi-Client Connection Tracking**:
  - Live display of all connected clients
  - Connection time, IP address, selected train
  - Per-client settings editable from UI
  
- **QR Code Generation**: 
  - Auto-generated QR code with AppLink
  - Scan to connect instantly with TRViS
  
- **Sample Data**: Pre-loaded with 3 sample trains for testing
- **Web UI**: Complete management interface with real-time updates

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

### Quick Start Test

1. **Start the demo server**:
   ```bash
   cd TRViS.DemoServer
   dotnet run
   ```

2. **Open browser** to `http://localhost:5000` to access the management interface

3. **Scan QR code** with TRViS or manually connect to `ws://localhost:5000/ws`

### Comprehensive Testing

#### 1. Feature Detection
- TRViS automatically calls `GetFeatures` on connection
- Server responds with `["SyncedData", "Timetable", "TrainSearch"]`
- Toggle "Train Search Enabled" in web UI to test feature detection
- Reconnect TRViS to see updated feature list

#### 2. Train Search
- In TRViS, open QuickSwitchPopup → "Search" tab
- Enter train number: `1234`, `5678`, or `9999`
- Click "検索" button
- See results with train details

#### 3. Train Selection
- Click search result
- Confirm in dialog (shows train number, work, stations, times)
- Train timetable displays in TRViS
- "Hako" tab automatically hidden

#### 4. Return to Scheduled Train
- Click "所定列車に戻る" button
- Returns to original train
- "Hako" tab reappears

#### 5. Search History
- Search multiple different trains
- History tracks last 10 searches
- Duplicates automatically removed

#### 6. Time Simulation
- In web UI, select speed (1x/30x/60x)
- Click "Start" button
- Watch time advance at selected speed
- TRViS receives SyncedData with advancing time
- Stop/Reset as needed

#### 7. Position Simulation
- Connect TRViS client
- See client appear in "Connected Clients" table
- Adjust "Location (m)" value
- Toggle "CanStart" checkbox
- Click "Update" button
- TRViS receives updated SyncedData

#### 8. Multi-Client Testing
- Connect multiple TRViS instances
- Each appears in connection table
- Set different locations/CanStart for each
- All receive individual SyncedData broadcasts

#### 9. Timeout Testing
- Stop server while TRViS is running
- Try to search in TRViS
- See timeout error after 10 seconds

#### 10. Real-time Broadcasting
- With time simulation running
- Monitor TRViS receiving SyncedData every 100ms
- Location and CanStart values update in real-time

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

## Testing

The demo server includes comprehensive tests:

```bash
cd TRViS.DemoServer.Tests
dotnet test
```

**Test Coverage**:
- TimeSimulationServiceTests (10 tests)
- ConnectionManagerServiceTests (8 tests)
- TimetableServiceTests (8 tests)
- **Total: 26 tests, all passing ✅**

## Future Enhancements

Potential additional features:
- ⏳ HTTP REST API endpoints (alternative to WebSocket)
- ⏳ Real-time timetable editing from web UI
- ⏳ Auto-sync timetable changes to connected clients
- ⏳ Persistent storage (database) for timetable data
- ⏳ Authentication and authorization
- ⏳ Multi-language support

## Development

To add more trains, edit `TimetableService.InitializeSampleData()` in `Services/TimetableService.cs`.

To modify protocol handling, edit `WebSocketHandler.ProcessMessageAsync()` in `Services/WebSocketHandler.cs`.
