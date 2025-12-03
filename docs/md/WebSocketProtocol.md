# TRViS WebSocket Protocol Specification

This document describes the WebSocket communication protocol between TRViS client and server.

## Connection

The client connects to the server using WebSocket protocol. After connection is established, both client and server can send messages at any time.

## Message Format

All messages are JSON objects sent as text frames. Each message should contain a `MessageType` field to identify the type of message.

```json
{
  "MessageType": "MessageTypeName",
  ...other fields...
}
```

## Message Types

### 1. Feature Discovery

#### 1.1 Feature List Request (Client → Server)

Sent by the client to request the list of features supported by the server.

```json
{
  "MessageType": "GetFeatures"
}
```

#### 1.2 Feature List Response (Server → Client)

Sent by the server in response to a feature list request.

```json
{
  "MessageType": "Features",
  "Features": ["TrainSearch", "SyncedData", "Timetable"]
}
```

**Supported Features:**
- `TrainSearch`: Server supports train search by train number
- `SyncedData`: Server supports real-time location/time synchronization
- `Timetable`: Server supports sending timetable data

### 2. Location/Time Synchronization

#### 2.1 Synced Data (Server → Client)

Sent periodically by the server to update the client's location and time information.

```json
{
  "MessageType": "SyncedData",
  "Location_m": 1234.5,
  "Time_ms": 3600000,
  "CanStart": true
}
```

**Fields:**
- `Location_m` (number, nullable): Current location in meters. Can be `null` if location is unknown.
- `Time_ms` (number): Current time in milliseconds since midnight.
- `CanStart` (boolean): Whether the train is ready to start.

#### 2.2 ID Update (Client → Server)

Sent by the client when the user changes the selected WorkGroup, Work, or Train.

```json
{
  "WorkGroupId": "work_group_1",
  "WorkId": "work_1",
  "TrainId": "train_1"
}
```

**Fields:**
- `WorkGroupId` (string, optional): Selected work group ID
- `WorkId` (string, optional): Selected work ID
- `TrainId` (string, optional): Selected train ID

### 3. Timetable Data

#### 3.1 Timetable Update (Server → Client)

Sent by the server to update timetable data. The scope of the update is determined by which ID fields are present.

```json
{
  "MessageType": "Timetable",
  "Scope": "Train",
  "WorkGroupId": "work_group_1",
  "WorkId": "work_1",
  "TrainId": "train_1",
  "Data": { ...timetable JSON... }
}
```

**Scope Values:**
- `All`: Complete timetable data for all work groups
- `WorkGroup`: Timetable data for a specific work group
- `Work`: Timetable data for a specific work
- `Train`: Timetable data for a specific train

**Fields:**
- `WorkGroupId` (string, optional): Target work group ID
- `WorkId` (string, optional): Target work ID
- `TrainId` (string, optional): Target train ID
- `Data`: Timetable JSON data (format depends on scope)

### 4. Train Search (New Feature)

#### 4.1 Train Search Request (Client → Server)

Sent by the client to search for trains by train number.

```json
{
  "MessageType": "SearchTrain",
  "TrainNumber": "1234",
  "RequestId": "unique-request-id"
}
```

**Fields:**
- `TrainNumber` (string, required): The train number to search for
- `RequestId` (string, required): Unique identifier for this request, used to match with response

#### 4.2 Train Search Response (Server → Client)

Sent by the server in response to a train search request.

**Success Response:**
```json
{
  "MessageType": "SearchTrainResult",
  "RequestId": "unique-request-id",
  "Success": true,
  "Results": [
    {
      "TrainId": "train_123",
      "TrainNumber": "1234",
      "WorkId": "work_1",
      "WorkName": "行路1",
      "WorkGroupId": "work_group_1",
      "StartStation": "東京",
      "EndStation": "大阪",
      "StartTime": "09:00",
      "EndTime": "12:30",
      "Direction": 0,
      "Destination": "大阪"
    }
  ]
}
```

**Error Response:**
```json
{
  "MessageType": "SearchTrainResult",
  "RequestId": "unique-request-id",
  "Success": false,
  "ErrorMessage": "Train not found"
}
```

**Fields:**
- `RequestId` (string, required): Matches the request ID from the search request
- `Success` (boolean, required): Whether the search was successful
- `Results` (array, optional): Array of matching train results (only present when Success is true)
- `ErrorMessage` (string, optional): Error description (only present when Success is false)

**Result Object Fields:**
- `TrainId` (string, required): Unique train ID
- `TrainNumber` (string, required): Train number
- `WorkId` (string, required): Work ID this train belongs to
- `WorkName` (string, optional): Human-readable work name
- `WorkGroupId` (string, optional): Work group ID
- `StartStation` (string, optional): Starting station name
- `EndStation` (string, optional): Ending station name
- `StartTime` (string, optional): Starting time (HH:mm format)
- `EndTime` (string, optional): Ending time (HH:mm format)
- `Direction` (number, optional): Train direction
- `Destination` (string, optional): Destination station

#### 4.3 Train Data Request (Client → Server)

After the user selects a train from search results, the client can request the full timetable data.

```json
{
  "MessageType": "GetTrainData",
  "TrainId": "train_123",
  "WorkId": "work_1",
  "RequestId": "unique-request-id"
}
```

**Fields:**
- `TrainId` (string, required): The train ID to retrieve
- `WorkId` (string, optional): The work ID (helps server locate the data faster)
- `RequestId` (string, required): Unique identifier for this request

#### 4.4 Train Data Response (Server → Client)

The server responds with full train data, similar to a Timetable message.

```json
{
  "MessageType": "TrainData",
  "RequestId": "unique-request-id",
  "Success": true,
  "TrainId": "train_123",
  "WorkId": "work_1",
  "Data": { ...train JSON data... }
}
```

**Fields:**
- `RequestId` (string, required): Matches the request ID
- `Success` (boolean, required): Whether the retrieval was successful
- `TrainId` (string, optional): The train ID
- `WorkId` (string, optional): The work ID
- `Data` (object, optional): Full train data in JSON format (see train.schema.json)
- `ErrorMessage` (string, optional): Error description (only when Success is false)

## Client Behavior

### Timeout Handling

The client should implement timeout handling for all request-response operations:

- **SearchTrain**: 10 seconds timeout
- **GetTrainData**: 15 seconds timeout
- **GetFeatures**: 5 seconds timeout

If a response is not received within the timeout period, the client should:
1. Display an appropriate error message to the user
2. Cancel the operation
3. Not retry automatically (user can manually retry)

### Feature Detection

1. On connection, the client should send a `GetFeatures` request
2. If the server doesn't respond within 5 seconds, assume the server doesn't support feature discovery
3. The client can still try other operations, but should handle gracefully if they're not supported

### Train Search Workflow

1. User enters train number in QuickSwitchPopup
2. Client sends `SearchTrain` request with a unique RequestId
3. Client waits for `SearchTrainResult` (max 10 seconds)
4. If results found, display list for user to select
5. User selects a train and confirms
6. Client sends `GetTrainData` request
7. Client waits for `TrainData` response (max 15 seconds)
8. Display the retrieved train timetable

### Returning to Scheduled Train

When displaying a searched train:
- Client remembers the original Work/Train that was active
- "Return to scheduled train" button is shown
- Clicking it restores the original Work/Train selection
- This state is maintained even if user searches for another train

## Server Implementation Notes

Servers implementing this protocol should:

1. Support feature discovery by responding to `GetFeatures`
2. Respond to unknown message types with an error or ignore them
3. Include the `RequestId` in all responses to match requests
4. Handle missing or malformed requests gracefully
5. Validate train numbers before searching
6. Return empty results array if no trains match (not an error)
7. Cache frequently accessed train data for performance

## Version History

- **v1.0** (2025-12-03): Initial specification with train search feature
