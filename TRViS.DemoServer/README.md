# TRViS Demo Server

A Blazor Server application for testing and demonstrating the TRViS train search functionality.

## Features

- **WebSocket Server**: Implements the TRViS WebSocket protocol for real-time communication
- **Train Search**: Supports searching trains by train number
- **Feature Discovery**: Responds to GetFeatures requests
- **Timetable Management**: Simple in-memory timetable data management
- **Time/Location Sync**: Simulates train location and time updates
- **Configurable Speed**: Supports 1x, 30x, and 60x time speeds
- **Train Search Toggle**: Can enable/disable train search feature for testing

## Running the Server

```bash
cd TRViS.DemoServer
dotnet run
```

Navigate to `https://localhost:5001` in your browser.

## Connecting from TRViS

Use the WebSocket URL: `ws://localhost:5000/ws` or `wss://localhost:5001/ws`

## Configuration

Edit `appsettings.json` to configure:
- Server ports
- Default time speed
- Feature flags

## Protocol Implementation

The server implements the protocol specified in `/docs/md/WebSocketProtocol.md`:
- SearchTrain messages
- GetTrainData messages  
- GetFeatures messages
- SyncedData broadcasts
- Timetable updates

## Sample Data

The server includes sample timetable data for testing. You can modify this data through the web UI.
