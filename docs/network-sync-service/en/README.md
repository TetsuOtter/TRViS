# NetworkSyncService — External System Integration (English)

> Supported protocol version: **1.1**
> Audience: developers implementing an external server that integrates with TRViS
> 日本語: [../ja/README.md](../ja/README.md)

---

## 1. Overview

`NetworkSyncService` is the mechanism by which TRViS (the client) receives
**operation-sync data, timetables, and various remote commands** from an
external system (the server). Two transports are supported:

| Transport | Scheme | Model | Typical use |
|---|---|---|---|
| **HTTP** | `http://` / `https://` | Client polling | Minimal: location, time, service-availability only |
| **WebSocket** | `ws://` / `wss://` | Server push (event driven) | Full integration incl. timetable & remote control |

TRViS automatically selects the transport from the URI scheme
(`ws`/`wss` → WebSocket, otherwise HTTP). A single connection uses exactly
one transport; HTTP and WebSocket are never mixed on the same connection.

## 2. Document structure

| File | Content |
|---|---|
| [common-data-model.md](common-data-model.md) | **Read first.** Data model shared by both transports (`SyncedData`, `Time_ms`, `CanStart`, `Location_m` & lat/lon fallback) |
| [http.md](http.md) | HTTP protocol detail (preflight, polling, request/response, failure handling) |
| [websocket.md](websocket.md) | WebSocket protocol detail (connection, framing, message discrimination, keep-alive, reconnection) |
| [server-to-client-messages.md](server-to-client-messages.md) | Full server→client message catalog (every `MessageType`, fields, parse rules, examples) |
| [client-to-server-messages.md](client-to-server-messages.md) | Client→server message catalog (ID update, requests) |
| [timetable.md](timetable.md) | Timetable delivery deep-dive (scope resolution, cache rebuild, location reset) |

Recommended reading order: **common-data-model → the transport you
implement (http or websocket) → server-to-client-messages →
client-to-server-messages → timetable**

## 3. Capability matrix

HTTP is a **strict subset** of WebSocket. Over HTTP only sync data
(location, time, service-availability) is available. For timetable delivery
or remote commands you must implement WebSocket.

| Capability | HTTP | WebSocket | Detail |
|---|:---:|:---:|---|
| Location sync (`Location_m`) | ✅ | ✅ | [common-data-model](common-data-model.md) |
| Time sync (`Time_ms`) | ✅ | ✅ | [common-data-model](common-data-model.md) |
| Service availability / auto-start (`CanStart`) | ✅ ※3 | ✅ | [common-data-model](common-data-model.md#4-meaning-of-canstart) |
| Lat/Lon fallback | ❌ ※1 | ✅ | [common-data-model](common-data-model.md) |
| Timetable delivery | ❌ | ✅ | [timetable](timetable.md) |
| Server info (ServerInfo) | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| Diagram info (DiagramInfo) | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| Select train (SelectTrain) | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| Operation command | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| Header color (HeaderColor) | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| Notification | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| Time format (TimeFormat) | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| Train search (SearchTrain) | ❌ | ✅ ※4 | [client-to-server-messages](client-to-server-messages.md#4-searchtrain) |
| Client→server ID notification | ✅ ※2 | ✅ | per-transport docs |

- ※1: The HTTP client **ignores** lat/lon even if present in the
  response (it parses only `Location_m` / `Time_ms` / `CanStart`).
- ※2: HTTP uses query parameters; WebSocket uses a JSON message.
- ※3: `CanStart` is the same value as `CanUseService`. **Auto-start of
  operation happens only over WebSocket**; over HTTP, even `CanStart`
  `true` does not auto-start operation
  ([common-data-model §4](common-data-model.md#4-meaning-of-canstart)).
- ※4: Train search is an **optional** capability negotiated via the
  `Features` array of
  [`ServerInfo`](server-to-client-messages.md#3-serverinfo): a server
  advertises `"TrainSearch"` to enable it. It is not tied to the protocol
  version number and is backward-compatible (a server that does not
  advertise the feature is simply not asked). See
  [`SearchTrain`](client-to-server-messages.md#4-searchtrain) /
  [`SearchTrainResponse`](server-to-client-messages.md#12-searchtrainresponse) /
  [`RequestTrainTimetable`](client-to-server-messages.md#5-requesttraintimetable).

## 4. Security (important)

**The protocol itself has no authentication or authorization.** Messages
on the wire are anonymous and the origin is not verified. In production
you must provide the following separately on the implementation side:

- Transport encryption via TLS (`https://` / `wss://`)
- Access control via a reverse proxy or WAF
- Authorization via URI query/path, TLS client certificates, etc.
  (TRViS uses the configured URI verbatim, so a token can be embedded in
  the URI — query for HTTP, or `ws(s)://host/path?token=...` for WebSocket)

TRViS trusts and executes delivered commands (`SelectTrain` /
`OperationCommand` / `HeaderColor`, etc.). A third party able to
impersonate the server and send these is a serious risk, so transport
protection is mandatory.

## 5. Protocol version

The `ProtocolVersion` field of the `ServerInfo` message is the
handshake-level signal of protocol compatibility. The protocol this
document set targets is **`"1.1"`**. The client currently does not reject
connections based on this value, but returning a correct value is
recommended for future compatibility negotiation.

**What changed in 1.1.** 1.1 adds the optional **train-search** feature
([`SearchTrain`](client-to-server-messages.md#4-searchtrain) →
[`SearchTrainResponse`](server-to-client-messages.md#12-searchtrainresponse),
then [`RequestTrainTimetable`](client-to-server-messages.md#5-requesttraintimetable)).
All additions are **backward-compatible**: unknown `MessageType` values
and optional fields are ignored per the existing rules, so a 1.0 server
keeps working unchanged. Availability of the feature is **negotiated via
the [`Features`](server-to-client-messages.md#3-serverinfo) array on
`ServerInfo`** (advertise `"TrainSearch"`), **not** via the version
number — the version bump is informational.
