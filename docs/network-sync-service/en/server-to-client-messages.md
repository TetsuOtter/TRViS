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
| `NavigateToHome` | Navigate to home screen | [§10](#10-navigatetohome) |
| `OpenTimetable` | Open timetable view for a specified train | [§11](#11-opentimetable) |
| `SearchTrainResponse` | Result of a train-search request | [§12](#12-searchtrainresponse) |
| `DeleteNotification` | Delete an already-delivered notification by Id | [§13](#13-deletenotification) |
| `DefaultSound` | Set default received/approach sound for notifications | [§14](#14-defaultsound) |

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
  "ProtocolVersion": "1.1",     // string | null
  "Features": ["TrainSearch"]   // string[] | null. Optional. Omitted/null = no extended features.
}
```

| Field | Type | Description |
|---|---|---|
| `Name` | string | Server name. |
| `Admin` | string | Admin / contact. |
| `Version` | string | Server implementation version. |
| `ProtocolVersion` | string | Supported protocol version. Currently `"1.1"`. |
| `Features` | string[] | Optional. Feature-id strings the server supports. Only string elements are kept; non-string elements are ignored. Absent/`null` means the server advertises no extended features. |

Each field `null` or missing means "unset". `ProtocolVersion` is the
handshake-level signal of protocol compatibility (optional capabilities
are negotiated separately via `Features`, below), so returning a correct
value is recommended.

**Feature negotiation.** `Features` advertises optional capabilities
that are negotiated independently of the version number. Known feature
ids so far:

| Feature id | Meaning |
|---|---|
| `"TrainSearch"` | The server supports the train-search request/response pair ([`SearchTrain`](client-to-server-messages.md#4-searchtrain) → [`SearchTrainResponse`](#12-searchtrainresponse)) and the follow-up [`RequestTrainTimetable`](client-to-server-messages.md#5-requesttraintimetable). |

The client sends [`RequestServerInfo`](client-to-server-messages.md#2-requestserverinfo)
automatically right after the WebSocket connection opens, so it learns
`Features` without any extra action, and uses it to decide whether to
show its train-search UI.

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
  "OrderNumber": "Order No. 3",           // string | null (dispatch/order number)
  "Title": "Service suspended",           // string | null
  "Summary": "Service suspended",         // string | null (compact-banner summary)
  "Body": "Due to strong winds...",       // string | null (BBCode allowed)
  "Priority": 1,                          // integer (0=normal,1=important, server-defined)
  "IssuedAt": "2024-03-01T09:00:00+09:00",// string (ISO 8601, TZ offset optional) | null
  "Receiver": "Crew of Train XX",         // string | null (recipient)
  "Sender": "Dispatch Center",            // string | null (sender/dispatcher)
  "IconText": "D",                        // string | null (icon glyph; used only if IconImageBase64 is unset)
  "IconColor_RGB": "#C62828",              // integer (0xRRGGBB) | string ("#RRGGBB") | omitted (icon background color; has a default)
  "IconImageBase64": null,                // string | null (icon image; takes priority over IconText when set)
  "Acknowledged": false,                  // boolean (optional). true if already acknowledged
  "CompactDisplay": false,                // boolean (optional). true = show initially as a small top banner
  "SectionStartStation": "Ishikawa",      // string | null (section start; station name or station ID)
  "SectionEndStation": "Kawabe",          // string | null (section end; station name or ID. single station if omitted)
  "StationsBefore": 1,                    // integer (optional, default 1). how many stations before the section to re-display
  "ReceivedSoundBase64": null,            // string | null (per-notification received sound; see §14)
  "ReceivedSoundFormat": null,            // "wav" | "mp3" | null
  "ApproachSoundBase64": null,            // string | null (per-notification approach sound; see §14)
  "ApproachSoundFormat": null             // "wav" | "mp3" | null
}
```

| Field | Type | Description |
|---|---|---|
| `Id` | string | Notification identifier. Serves as the key for acknowledgement ([`AcknowledgeNotification`](client-to-server-messages.md#6-acknowledgenotification)). |
| `OrderNumber` | string | Dispatch/order number. Display only (an operational management number owned by the server/dispatcher). |
| `Title` | string | Heading. Used by the large popup. |
| `Summary` | string | Optional summary for the compact banner. When omitted or empty, the client falls back to `Title`; the large popup always uses `Title`. |
| `Body` | string | Body text. **BBCode** (`[b]…[/b]`, etc.) may be used. Rendering is up to the client implementation. |
| `Priority` | integer | Importance. Accepted only as a JSON number readable as a 32-bit integer, default `0`. Meaning is server-defined. |
| `IssuedAt` | string | Issue time. A string with a date (`yyyy-MM-dd`) and optional ISO 8601 time/offset is parsed as a date/time. Whether a TZ offset (`Z` or `+HH:mm`/`-HH:mm`) is present changes interpretation (see below). If it cannot be parsed in this form, the client keeps the original string for display instead of interpreting it as a date/time. |
| `Receiver` | string | Recipient. Display only. |
| `Sender` | string | Sender/dispatcher. Display only. |
| `IconText` | string | A short (1-2 character) glyph shown as the icon. Ignored when `IconImageBase64` is set. |
| `IconColor_RGB` | integer \| string | Background color for `IconText`. Accepted either as a **JSON number** (`0xRRGGBB` in decimal, readable as a 32-bit integer) or as a **`"#RRGGBB"` string** (leading `#` required, case-insensitive). Unset (default color) if neither form parses. A client default is used when omitted. |
| `IconImageBase64` | string | Base64-encoded icon image binary (may include a data URI prefix such as `data:image/png;base64,...`). Takes priority over `IconText`/`IconColor_RGB` when set. |
| `Acknowledged` | boolean | Optional. Indicates whether the server considers this client to have already acknowledged this notification. Treated as acknowledged only on JSON `true`; otherwise (`false`/missing) unacknowledged. Used when re-delivering notifications (e.g. after reconnect) so already-acknowledged ones are marked read (the client does not re-popup a read notification). |
| `CompactDisplay` | boolean | Optional. Only on JSON `true`, the initial display is a small banner at the top of the screen (otherwise/missing: the large centered popup). The acknowledge button is still shown on the small banner, and an acknowledge-required notification (has `Id`) stays until acknowledged. |
| `SectionStartStation` | string | Optional. The start of the section/station this notification targets, given as a **station name or station ID** (resolved by station-ID match, then station-name match; case-sensitive). After it is acknowledged and hidden, the notification is automatically re-shown (as an acknowledged small banner) once the train reaches `StationsBefore` stations before this section, and auto-hidden after leaving the section. Not re-shown if no matching station exists on the current train's route. |
| `SectionEndStation` | string | Optional. The end of the section, given as a **station name or station ID**. When omitted, treated as the same single station as `SectionStartStation`. Section direction (which end comes first) does not matter (normalized by route index). |
| `StationsBefore` | integer | Optional, default `1`. How many stations before the section start to begin re-display. Accepted only as a JSON number readable as a 32-bit integer. Values ≤ 0 are treated as 0 (shown from the section start station). |
| `ReceivedSoundBase64` | string | Optional. Base64-encoded binary of the sound to play when this notification is first shown (may include a data URI prefix such as `data:audio/mpeg;base64,...`). If unset/null, the client falls back to the received-sound default set via [`DefaultSound`](#14-defaultsound) (§14), if any; otherwise silent. |
| `ReceivedSoundFormat` | string | Optional. Format of `ReceivedSoundBase64`. Use `"wav"` or `"mp3"` (always supported by the client). Any other value fails to decode/play and is treated as silent. |
| `ApproachSoundBase64` | string | Optional. Base64-encoded binary of the sound to play when this notification is re-shown as a section-linked small banner (approaching the section). If unset/null, falls back to the approach-sound default via [`DefaultSound`](#14-defaultsound) (§14); otherwise silent. |
| `ApproachSoundFormat` | string | Optional. Format of `ApproachSoundBase64`. Same rules as `ReceivedSoundFormat`. |

**Section/station-linked re-display:**
- A notification with `SectionStartStation` (and optionally `SectionEndStation`) is hidden once acknowledged, but is re-shown as an acknowledged small banner when the train reaches `StationsBefore` stations before the target section (or single station), and auto-hidden after leaving it.
- Stations may be given by **name or ID**. The station ID is the normalized station ID in the SQLite format, or each row's `Id` in the JSON format (a per-row Id suffices since resolution is within the current train's route). Either format can fall back to station-name matching.

**How TZ presence in `IssuedAt` affects display:**
- With a TZ offset (e.g. `2024-03-01T09:00:00+09:00` or `...Z`): the client converts and displays the time **accounting for the device's current TZ**.
- Without a TZ offset (e.g. `2024-03-01T09:00:00`): the client displays the time **as-is**, with no TZ conversion.
- A date-only string such as `2024-03-01` is accepted and displayed as-is (no TZ conversion).
- A string without the `T` date-time separator that is not date-only (for example,
  `2024-03-01 09:00:00`) is not parsed as a date/time; the original string is
  displayed as-is. Other arbitrary/unparseable strings behave the same way.

For acknowledgement (client → server), see
[`AcknowledgeNotification`](client-to-server-messages.md#6-acknowledgenotification).

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

## 10. NavigateToHome

Instructs the client to navigate to the home (start) screen.
No additional fields are needed; the payload contains only `MessageType`.

```jsonc
{
  "MessageType": "NavigateToHome"
}
```

Only `MessageType` is present. On receipt the client navigates to the
home screen immediately. Operation state and train selection are not
affected by this command; use other server-driven commands to control
those independently.

---

## 11. OpenTimetable

Selects the specified train and opens the timetable view (D-TAC screen,
"時刻表" tab) directly. On receipt the client:

1. Applies the train selection (WorkGroupId / WorkId / TrainId) — same
   rules as [`SelectTrain`](#5-selecttrain).
2. Navigates to the D-TAC screen if not already there.
3. Switches to the timetable (VerticalView) tab.

```jsonc
{
  "MessageType": "OpenTimetable",
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
are ignored). A `null` or omitted field leaves that selection level
unchanged.

---

## 12. SearchTrainResponse

Reply to a client [`SearchTrain`](client-to-server-messages.md#4-searchtrain)
request. Available only when the server advertises the `TrainSearch`
[feature](#3-serverinfo). The message echoes the request's `RequestId`
so the client can correlate it with the request it sent.

```jsonc
{
  "MessageType": "SearchTrainResponse",
  "RequestId": "3f2a...unique...",   // echoes the SearchTrain RequestId (required for correlation)
  "Results": [
    {
      "WorkGroupId": "wg-1",
      "WorkId": "w-1",
      "TrainId": "t-1",
      "TrainNumber": "1234",
      "WorkName": "1行路",
      "Direction": 1,                 // integer | null. -1 = Inbound, 1 = Outbound
      "StartStationName": "東京",
      "StartTime": "09:00",
      "EndStationName": "大阪",
      "EndTime": "12:30"
    }
    // ... zero or more candidates. The same train number may yield
    //     multiple candidates (different works/trains).
  ]
}
```

| Field | Type | Description |
|---|---|---|
| `RequestId` | string | Echo of the [`SearchTrain.RequestId`](client-to-server-messages.md#4-searchtrain). Required — the client correlates the response by this value. |
| `Results` | object[] | Always present. Array of matching candidates. An **empty array means "no matching train"** (a successful response, not a timeout). |

Each element of `Results` is a summary used to (a) render the candidate
list and (b) show a confirmation dialog. It does **not** include the full
timetable rows — those are fetched separately via
[`RequestTrainTimetable`](client-to-server-messages.md#5-requesttraintimetable).

| Result field | Type | Description |
|---|---|---|
| `WorkGroupId` | string | Id needed to fetch & display the train. |
| `WorkId` | string | Id needed to fetch & display the train. |
| `TrainId` | string | Id needed to fetch & display the train. |
| `TrainNumber` | string | The train number. |
| `WorkName` | string | Name of the work this train belongs to. |
| `Direction` | integer \| null | `-1` = Inbound, `1` = Outbound. |
| `StartStationName` | string | Start station name. |
| `StartTime` | string | Start time as a display string (e.g. `"09:00"`). |
| `EndStationName` | string | End station name. |
| `EndTime` | string | End time as a display string (e.g. `"12:30"`). |

- `Results` is **always present**; an empty array is a valid, successful
  "no results" reply. A server that advertises `TrainSearch` **must
  always respond** — even with zero results — so the client can
  distinguish "no results" from "no/failed response" (the client times
  out after 10s and reports an error when nothing arrives).
- Matching semantics are driven by the request's `MatchMode`
  (see [`SearchTrain`](client-to-server-messages.md#4-searchtrain)) —
  `Prefix` (default), `Contains`, or `Exact` — not server-defined.

---

## 13. DeleteNotification

Instructs the client to discard an already-delivered
[`Notification`](#8-notification) (§8) by `Id`, whether or not the
client has acknowledged it. Use this to retract a notification that is
no longer relevant (e.g. superseded, issued in error, or tied to a
train the crew has since left).

```jsonc
{
  "MessageType": "DeleteNotification",
  "Id": "n-001"   // string (required)
}
```

| Field | Type | Description |
|---|---|---|
| `Id` | string | Identifier of the notification to delete — the same value as the target `Notification.Id`. **Required**; a missing or empty `Id` is ignored (logged and no-op). |

- Removes the notification regardless of read/acknowledgement state —
  an unacknowledged notification is discarded too, not just
  already-acknowledged ones.
- If the notification is currently shown as the large popup or a small
  banner, the client dismisses it immediately. If it is only queued
  (not yet shown), it is removed from the queue instead. If no
  notification with that `Id` is currently held, the message is a no-op.
- An unknown `Id` (already deleted, expired, or never delivered) is not
  an error — the client simply has nothing to remove.

## 14. DefaultSound

Sets the default "received sound" (played when a notification is first
shown) and "approach sound" (played when a section-linked notification
is re-shown as the train approaches the target section) used when an
individual [`Notification`](#8-notification) (§8) does not specify its
own sound. Audio binaries are never bundled with the app, so sound must
always come from the server, either via this message or via the
per-notification fields.

This message **fully replaces both roles' defaults every time it is
sent** (not a diff). A role whose fields are unset/null has its default
reset to "none" (silent). The defaults are in-memory session state only
and are discarded on WebSocket disconnect (resend after reconnect if
still needed).

```jsonc
{
  "MessageType": "DefaultSound",
  "ReceivedSoundBase64": null, // string | null. Default received sound
  "ReceivedSoundFormat": null, // "wav" | "mp3" | null
  "ApproachSoundBase64": null, // string | null. Default approach sound
  "ApproachSoundFormat": null  // "wav" | "mp3" | null
}
```

| Field | Type | Description |
|---|---|---|
| `ReceivedSoundBase64` | string | Optional. Base64-encoded binary used as the default received sound. `null`/omitted clears this role's default. |
| `ReceivedSoundFormat` | string | Optional. Format of `ReceivedSoundBase64`. Use `"wav"` or `"mp3"` (see §8's note). |
| `ApproachSoundBase64` | string | Optional. Base64-encoded binary used as the default approach sound. `null`/omitted clears this role's default. |
| `ApproachSoundFormat` | string | Optional. Format of `ApproachSoundBase64`. |

**Resolution order (both received and approach sounds):**
1. If the individual `Notification` specifies a sound for that role, play it.
2. Otherwise, play the most recently received `DefaultSound` value for that role, if any.
3. Otherwise, silent.

**Size limit:** the decoded audio binary must be **16MiB (16 × 1024 ×
1024 bytes) or smaller** (applies to `ReceivedSoundBase64`/
`ApproachSoundBase64` in both this message and `Notification`). Since
Base64 inflates size by roughly 4/3, the field itself may be up to
roughly 21.3MiB. Audio exceeding this limit is discarded client-side and
never played — silently (no error is raised), simply treated as no
sound. This limit carries no particular significance; it is a practical
guard against unbounded binary payloads.

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
- `Latitude_deg`/`Longitude_deg`/`Accuracy_m`/`Color_RGB`/`Priority`/`Notification.StationsBefore`
  **must be JSON number type** (strings are invalid). `StationsBefore` defaults to `1` when missing.
- Each ID in `SelectTrain` **must be JSON string type**.
- `OperationCommand.Action` is **required** and only known values are
  valid (case-insensitive).
- `Notification.IssuedAt` is parsed when it has a `yyyy-MM-dd` date and an optional ISO 8601 time/offset. Invalid or non-date strings are displayed unchanged; a TZ offset changes whether the parsed value is converted (see [§8](#8-notification)).
- `ServerInfo.Features` is a **JSON array**; only string elements are
  kept and non-string elements are ignored (absent/`null` = no extended
  features).
- Unknown `MessageType`, missing `MessageType`, invalid JSON are
  **silently ignored**.
- `Notification`/`DefaultSound` sound fields (`*SoundBase64`) **must
  decode to 16MiB or less**. Anything over the limit, a decode failure,
  or an unsupported format is treated as silent — never an
  exception (see [§14](#14-defaultsound)).
