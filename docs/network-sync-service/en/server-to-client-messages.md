# Server → Client Message Catalog (English)

> [← Back to index](README.md) / Prerequisite: [common-data-model.md](common-data-model.md) /
> [websocket.md](websocket.md)
> 日本語: [../ja/server-to-client-messages.md](../ja/server-to-client-messages.md)

**WebSocket only.** Full spec of every message the server can push to
the client. Each message is a JSON object in a UTF-8 text frame and
must carry a `MessageType` field (exact case). Unknown/missing
`MessageType` is ignored.

| `MessageType` | Purpose | Section |
|---|---|---|
| `SyncedData` | Sync of location/time/service-availability (auto-start) | [§1](#1-synceddata) |
| `Timetable` | Timetable delivery | [§2](#2-timetable) |
| `ServerInfo` | Server information | [§3](#3-serverinfo) |
| `DiagramInfo` | Diagram information | [§4](#4-diagraminfo) |
| `SelectTrain` | Instruct train selection | [§5](#5-selecttrain) |
| `OperationCommand` | Instruct operation action | [§6](#6-operationcommand) |
| `HeaderColor` | Change header color | [§7](#7-headercolor) |
| `Notification` | Notification | [§8](#8-notification) |
| `TimeFormat` | Time display format | [§9](#9-timeformat) |

> Notation: "Required" means a field the server effectively needs to
> produce meaningful behavior. "Optional" may be omitted. A type
> mismatch is generally treated as "ignored (default value)" and never
> throws.

---

## 1. SyncedData

The most fundamental message: pushes location, time, and
service-availability. Over WebSocket it is processed immediately on
receipt (no buffering).

```jsonc
{
  "MessageType": "SyncedData",
  "Location_m": 1234.5,        // number | null. null = undetermined
  "Time_ms": 43200000,         // integer. ms since midnight that day
  "CanStart": true,            // boolean. default true
  "Latitude_deg": 35.681236,   // number | null (optional)
  "Longitude_deg": 139.767125, // number | null (optional)
  "Accuracy_m": 5.0            // number | null (optional)
}
```

| Field | Type | Default (missing) | Description |
|---|---|---|---|
| `Location_m` | number \| null | `null` (NaN) | Distance from start [m]. `null`/wrong type → NaN. |
| `Time_ms` | integer | `0` | Ms since midnight that day. |
| `CanStart` | boolean | **`true`** | Service availability / permission to auto-start operation (same value as `CanUseService`; over WS `true` auto-starts operation). |
| `Latitude_deg` | number | `null` | Latitude. Invalid unless number type. |
| `Longitude_deg` | number | `null` | Longitude. Invalid unless number type. |
| `Accuracy_m` | number | `null` | Positioning accuracy [m]. |

For field meanings and the effect on station detection, see the
[common data model](common-data-model.md).

## 2. Timetable

Delivers timetable data. The `Data` field embeds the timetable body in
[TRViS JSON format](https://github.com/TetsuOtter/TRViS/wiki/JSON%E5%BD%A2%E5%BC%8F%E3%81%AE%E3%83%87%E3%83%BC%E3%82%BF%E3%83%99%E3%83%BC%E3%82%B9)
as **raw JSON (an object/array, not a string)**.

```jsonc
{
  "MessageType": "Timetable",
  "WorkGroupId": "wg-1",   // optional (used for scope resolution)
  "WorkId": "w-1",         // optional (used for scope resolution)
  "TrainId": "t-1",        // optional (used for scope resolution)
  "Data": { /* or [...] : timetable body in TRViS JSON format */ }
}
```

| Field | Type | Description |
|---|---|---|
| `WorkGroupId` | string | Optional. Target WorkGroup. |
| `WorkId` | string | Optional. Target Work. |
| `TrainId` | string | Optional. Target Train. |
| `Data` | object \| array | Timetable body (raw JSON in TRViS JSON format). |

- Which IDs are present determines the scope (All / WorkGroup / Work /
  Train), and the type of `Data` matches the scope.
- Scope resolution rules, cache-rebuild behavior, and the location reset
  on the All scope are detailed in **[timetable.md](timetable.md)**.
- The contents of `Data` (the timetable body structure) are out of scope
  for this document set (see the TRViS JSON format wiki above).

## 3. ServerInfo

Information about the server itself. Can be sent as the response to a
client `RequestServerInfo`
([client-to-server-messages.md](client-to-server-messages.md)) or as a
server-initiated broadcast.

```jsonc
{
  "MessageType": "ServerInfo",
  "Name": "My Sync Server",     // string | null
  "Admin": "admin@example.com", // string | null
  "Version": "1.2.3",           // string | null
  "ProtocolVersion": "1.0"      // string | null
}
```

| Field | Type | Description |
|---|---|---|
| `Name` | string | Server name. |
| `Admin` | string | Admin / contact. |
| `Version` | string | Server implementation version. |
| `ProtocolVersion` | string | Supported protocol version. Currently `"1.0"`. |

Each field `null` or missing means "unset". `ProtocolVersion` is the
only signal of protocol compatibility, so returning a correct value is
recommended.

## 4. DiagramInfo

Information about a "diagram", the concept above WorkGroup. Can be sent
as the response to `RequestDiagramInfo` or as a server-initiated
broadcast.

```jsonc
{
  "MessageType": "DiagramInfo",
  "DiagramId": "d-1",                  // string | null (client-side Id)
  "Name": "Weekday diagram",           // string | null
  "Description": "March 2024 revision",// string | null
  "WorkGroupIds": ["wg-1", "wg-2"]     // string[] | null
}
```

| Field | Type | Description |
|---|---|---|
| `DiagramId` | string | Diagram identifier (internally `Id` on the client). |
| `Name` | string | Diagram name. |
| `Description` | string | Description / note. |
| `WorkGroupIds` | string[] | List of WorkGroup IDs in this diagram. Only string elements are kept. |

> The key on the wire is `DiagramId` (mapped to the client's internal
> `Id`). `WorkGroupIds` is a JSON array; non-string elements are ignored.

## 5. SelectTrain

Instructs the client to select a specific train. A `null`/omitted level
is left unchanged (supports partial specification for future
extension).

```jsonc
{
  "MessageType": "SelectTrain",
  "WorkGroupId": "wg-1",  // string | null
  "WorkId": "w-1",        // string | null
  "TrainId": "t-1"        // string | null
}
```

| Field | Type | Description |
|---|---|---|
| `WorkGroupId` | string | Optional. WorkGroup to select. |
| `WorkId` | string | Optional. Work to select. |
| `TrainId` | string | Optional. Train to select. |

Each field is only accepted when of **JSON string type** (numbers etc.
are ignored).

## 6. OperationCommand

An operation-related instruction.

```jsonc
{
  "MessageType": "OperationCommand",
  "Action": "StartOperation"   // required. one of the table (case-insensitive)
}
```

| `Action` value | Meaning |
|---|---|
| `StartOperation` | Start operation (enable location service, enter operating mode) |
| `EndOperation` | End operation |
| `EnableLocationService` | Enable the location service |
| `DisableLocationService` | Disable the location service |

- `Action` is **required**. If missing or empty, the message is ignored.
- The value is interpreted case-insensitively (e.g. `startoperation`
  works too).
- Unknown values not in the table are ignored.

## 7. HeaderColor

A request to change the title-bar (header) color.

```jsonc
{
  "MessageType": "HeaderColor",
  "ResetToDefault": false,   // boolean. true → revert to device default
  "Color_RGB": 16711680      // integer (0xRRGGBB). here red 0xFF0000
}
```

| Field | Type | Description |
|---|---|---|
| `ResetToDefault` | boolean | Only JSON `true` means "revert to default". Otherwise (`false`/missing) treated as false. |
| `Color_RGB` | integer | Integer in `0xRRGGBB` form. Ignored when `ResetToDefault=true`. |

- `ResetToDefault` is true strictly only on JSON `true`.
- `Color_RGB` is accepted only when it is a JSON number readable as a
  32-bit integer. `16711680` (= `0xFF0000`) is red.

## 8. Notification

A notification (arbitrary announcement). Delivered as a received event
(display details depend on the client implementation).

```jsonc
{
  "MessageType": "Notification",
  "Id": "n-001",                          // string | null
  "Title": "Service suspended",           // string | null
  "Body": "Due to strong winds...",       // string | null
  "Priority": 1,                          // integer (0=normal,1=important, server-defined)
  "IssuedAt": "2024-03-01T09:00:00+09:00" // string (ISO 8601) | null
}
```

| Field | Type | Description |
|---|---|---|
| `Id` | string | Notification identifier. |
| `Title` | string | Heading. |
| `Body` | string | Body text. |
| `Priority` | integer | Importance. Accepted only as a JSON number readable as a 32-bit integer, default `0`. Meaning is server-defined. |
| `IssuedAt` | string | Issue time. **ISO 8601** (round-trippable form, e.g. `2024-03-01T09:00:00+09:00`). Unset if unparseable. |

## 9. TimeFormat

Specifies the title-bar time display format.

```jsonc
{
  "MessageType": "TimeFormat",
  "Format": "HH:mm:ss"   // string | null. null/omitted → reset to device default
}
```

| Field | Type | Description |
|---|---|---|
| `Format` | string | e.g. `"HH:mm:ss"` / `"HH:mm"`. `null` or omitted → reset to device default. |

The format string is interpreted per the client's time formatter.

---

## Appendix: parsing behavior summary

Common pitfalls for external implementers:

- **Envelope keys are case-sensitive** (`MessageType`, `Location_m`,
  etc.). Only the JSON inside `Timetable`'s `Data` (timetable body) is
  case-insensitive.
- **Wrong-type fields are generally "ignored = default"**, never an
  exception. Send correct JSON types to reliably deliver values.
- `SyncedData.CanStart` **defaults to `true` when omitted**. It means
  "service availability / permission to auto-start operation" and **over
  WS `true` auto-starts operation**. To avoid unintentionally starting
  operation, send an explicit `false`
  ([common-data-model §4](common-data-model.md#4-meaning-of-canstart)).
- `Latitude_deg`/`Longitude_deg`/`Accuracy_m`/`Color_RGB`/`Priority`
  **must be JSON number type** (strings are invalid).
- Each ID in `SelectTrain` **must be JSON string type**.
- `OperationCommand.Action` is **required** and only known values are
  valid (case-insensitive).
- `Notification.IssuedAt` is **ISO 8601** only.
- Unknown `MessageType`, missing `MessageType`, invalid JSON are
  **silently ignored**.
