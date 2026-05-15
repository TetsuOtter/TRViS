# Common Data Model (English)

> [← Back to index](README.md) / 日本語: [../ja/common-data-model.md](../ja/common-data-model.md)

The core data structures shared by both HTTP and WebSocket.
**Read this before any other document.**

---

## 1. SyncedData

The core operation-sync object: location, time, and service
availability (= permission to auto-start operation; see
[§4](#4-meaning-of-canstart)). Over HTTP it is the body of the polling
response; over WebSocket it is
the body of a `SyncedData` message.

### 1.1 Field definitions

| Field | JSON type | Required | Transport | Description |
|---|---|:---:|---|---|
| `Location_m` | number \| null | optional | HTTP / WS | Train position (distance from start) [m]. `null` = "distance undetermined". |
| `Time_ms` | integer | optional | HTTP / WS | **Milliseconds elapsed since 00:00:00 of that day.** Not a UNIX epoch. |
| `CanStart` | boolean | optional | HTTP / WS | Location-service availability / permission to auto-start operation (same value as `CanUseService`). **Over WebSocket `true` auto-starts operation.** See [§4](#4-meaning-of-canstart). |
| `Latitude_deg` | number \| null | optional | **WS only** | Latitude [deg]. |
| `Longitude_deg` | number \| null | optional | **WS only** | Longitude [deg]. |
| `Accuracy_m` | number \| null | optional | **WS only** | Positioning accuracy of the lat/lon [m]. |

> The HTTP client **does not parse** `Latitude_deg` / `Longitude_deg` /
> `Accuracy_m`. If you need station detection using lat/lon, use WebSocket.

### 1.2 Defaults on missing / type-mismatched fields

Every field is optional. The client parsing behavior is as follows; it
**never throws** — if a field is missing or of the wrong type it falls
back to a safe default.

| Field | Key missing | Explicit `null` | Wrong number/type |
|---|---|---|---|
| `Location_m` | `null` (NaN, undetermined) | `null` (NaN) | `null` (NaN) |
| `Time_ms` | `0` | `0` | `0` |
| `CanStart` | **`true`** | **`true`** | **`true`** |
| `Latitude_deg` | `null` | `null` | `null` (invalid unless number type) |
| `Longitude_deg` | `null` | `null` | `null` (invalid unless number type) |
| `Accuracy_m` | `null` | `null` | `null` (invalid unless number type) |

> **Important — `CanStart` defaults to `true`**
> "Not available" is treated as a special state, so omitting `CanStart`
> means **available (`true`)**. `CanStart` does not mean "departure
> permitted"; it means "location-service availability / permission to
> auto-start operation", and **over WebSocket `true` auto-starts
> operation** (see [§4](#4-meaning-of-canstart)). To avoid
> unintentionally starting operation you must explicitly send `false`.

> **Important — `Latitude_deg` etc. must be JSON number type**
> A value like the string `"35.0"` is invalid (treated as `null`).
> Always send a numeric literal `35.0`.

### 1.3 Representing "distance undetermined" in JSON

The "distance not yet determined" state is represented by **JSON `null`**.

- `NaN` is invalid JSON and must not be used.
- When the server sends `Location_m: null`, TRViS converts it internally
  to `NaN` and treats it as "distance undetermined".
- The reference server likewise outputs `null` in JSON when its internal
  state is undetermined (NaN).

---

## 2. Station detection: `Location_m` and lat/lon fallback

TRViS determines "which station am I at / running toward the next
station" from the received location and updates the display. The
branching is:

```mermaid
flowchart TD
    A[SyncedData received] --> B{Location_m a<br/>valid number?}
    B -- yes --> C[Determine station by Location_m<br/>lat/lon history reset]
    B -- no null/NaN --> D{Latitude_deg and Longitude_deg<br/>both valid numbers?}
    D -- yes --> E[Determine station by lat/lon<br/>WebSocket-only path]
    D -- no --> F[Skip location update]
```

### 2.1 `Location_m`-based detection

When `Location_m` is a valid number, the current station or "running to
next station" is determined using each station's configured position and
detection radius (derived from the timetable data). On this path the
lat/lon moving-average history is reset.

### 2.2 Lat/lon fallback (WebSocket only)

When `Location_m` is `null` (internally `NaN`) and both `Latitude_deg`
and `Longitude_deg` are valid numbers, TRViS falls back to a lat/lon
station-detection algorithm (a heuristic using a moving average of the
last few distances).

- The HTTP client does not parse lat/lon, so it **never reaches** this path.
- The fallback assumes continuous positioning and keeps an internal
  distance history. A single isolated lat/lon fix may not trigger a
  station transition (by design it defers until the moving average fills).
- `Accuracy_m` is propagated to the receiving-side event as ancillary
  info but is not used as a threshold by the detection algorithm itself.

### 2.3 When neither is available

If `Location_m` is invalid and lat/lon are not both present, no
location-state update is performed (the previous station state is kept).
`Time_ms` and `CanStart` processing happens every time regardless of
this branch.

---

## 3. Meaning of `Time_ms`

`Time_ms` is the **milliseconds elapsed since midnight (00:00:00) of
that day**. It is **not** UNIX epoch seconds/milliseconds.

| Example (`Time_ms`) | Time represented |
|---|---|
| `0` | 00:00:00 |
| `43200000` | 12:00:00 |
| `86399000` | 23:59:59 |

- The client rounds to second precision (integer part of
  `Time_ms / 1000`) for time sync. Sub-second precision is effectively
  ignored.
- A time change is propagated downstream only when the value differs
  from the previous one (repeating the same value is idempotent).
- There is no date (year/month/day) concept. The protocol has no way to
  represent crossing midnight; it stays within the time-of-day domain.

---

## 4. Meaning of `CanStart`

> **Naming caveat**: `CanStart` is **not** a "may the train depart"
> (departure-permission) field. Its actual meaning is **"may the
> location service be made available / may operation be started
> automatically"**. Internally `CanStart` is set to the **same value**
> as `CanUseService` (the `ILocationService` "service availability"
> flag). `CanStart` and `CanUseService` always hold the same boolean.

`CanStart` is a server-driven flag for "may the client start/use
location-based tracking (operating mode)".

- The user's "Start operation" ("運行開始") button itself is **always
  pressable** regardless of `CanStart`. What `CanStart` controls is the
  **automatic** path below.
- The `CanStart` value is mirrored into `CanUseService`; a corresponding
  state change is propagated downstream when it transitions
  `false` ↔ `true`.
- The default when the field is missing is **`true`**
  ([§1.2](#12-defaults-on-missing--type-mismatched-fields)).

### 4.1 `CanStart` = `true` auto-starts operation (WebSocket only)

**Only over a WebSocket connection**, when `CanStart` transitions to
`true`, TRViS **automatically enables the location service and starts
operation** with no user action (the UI also auto-transitions to the
"running" state). This is an implementation business rule.

```mermaid
flowchart TD
    S["Server: SyncedData CanStart=true"] --> P[ProcessSyncedData]
    P --> C["CanStart=true / CanUseService=true<br/>(same value) → CanStartChanged fires"]
    C --> W{Current connection<br/>is WebSocket?}
    W -- yes --> A["Auto-enable location service<br/>(IsEnabled=true)"]
    A --> U["UI auto-starts operation<br/>(IsRunning=true / location ON)"]
    W -- no HTTP --> N["No auto-start<br/>(only reflected in CanUseService)"]
```

- **WebSocket**: the instant `CanStart` goes `false`→`true`, the
  location service auto-enables and operation auto-starts. The user does
  not need to press "Start operation". The server can put a client into
  operating mode with this single flag.
- **HTTP**: the auto-start above does **not** happen (the automation is
  limited to WebSocket connections). Over HTTP `CanStart` is only
  mirrored into `CanUseService`; starting operation is left to the
  user's button action.

### 4.2 Behavior of `CanStart` = `false`

- `CanUseService` becomes `false` (reflected e.g. in service-unavailable
  UI state).
- The auto-enable handler acts only on `true`. A `true`→`false`
  transition does **not** auto-stop operation via this path (`false`
  means "do not auto-start / not available", it is not a trigger that
  force-ends an operation in progress). Explicit stopping is done via
  `OperationCommand` (e.g. `EndOperation`) or user action.

### 4.3 Implications for server implementers

- To "not let operation start yet", explicitly send `CanStart: false`
  (omitting it defaults to `true` = available / auto-start permitted).
- **Beware serializer default-omission**: with a JSON library that does
  not emit a boolean's default (`false`), a message intended as
  `CanStart=false` ends up with the field **absent** on the wire, the
  client interprets the absence as the default `true`, and a WebSocket
  client unintentionally starts operation. Always emit `CanStart`
  explicitly regardless of value and verify the actual bytes.
- For a WebSocket client, understand the side effect that sending
  `CanStart: true` puts that client into operating mode automatically
  (sending `true` with the intent of "just show data" will
  unintentionally start operation).
- To control operation actively, consider combining `CanStart` with
  `OperationCommand` (`StartOperation` / `EndOperation` /
  `EnableLocationService` / `DisableLocationService`,
  [server-to-client-messages.md](server-to-client-messages.md#6-operationcommand)).
