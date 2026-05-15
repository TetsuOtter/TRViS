# TRViS AppLink Specification (English)

> Supported AppLink version: **1.0**
> Audience: developers generating URLs (links, QR codes, etc.) that launch TRViS
> 日本語: [../ja/README.md](../ja/README.md)

---

## 1. Overview

**AppLink** is a custom-URL-scheme deep link that launches the TRViS app
from outside and makes it load a specified timetable (and optionally
connect to a realtime sync server).

Basic URI shape:

```
trvis://app/<action>?<query>
```

- Scheme: `trvis://` (only this scheme is registered with the OS)
- Host: `app` (required)
- Action (path): `/open/json` or `/open/sqlite`
- Query: which resource to load (required, must not be empty)

Minimal example:

```
trvis://app/open/json?path=https://example.com/timetable.json
```

## 2. Document structure

| File | Content |
|---|---|
| [uri-format.md](uri-format.md) | **Read first.** Full URI grammar (scheme/host/action) and query parameters, encoding rules, precedence, file-type × scheme matrix, version |
| [resource-loading.md](resource-loading.md) | Per-resource-kind loading (`file` / `http(s)` / `ws(s)` / inline data), user confirmation gates, history, realtime integration (`rts`/`rtk`/`rtv`) |
| [platform-registration.md](platform-registration.md) | Registration on iOS / MacCatalyst / Android, the OS-to-in-app invocation pipeline, the custom-scheme-only nature |

Recommended reading order: **uri-format → resource-loading →
platform-registration**

## 3. What AppLink can do

| Use | How to specify | Kind |
|---|---|---|
| Open a JSON timetable on the web | `path=https://...` (or `http://`) | JSON only |
| Open a SQLite DB locally | `path=file://...` (SQLite is `file` only) | SQLite |
| Open a file in the on-device timetable folder | `local=relative/path` | JSON / SQLite |
| Embed timetable data in the URL | `data=<URL-safe Base64>` | JSON only |
| Receive timetable + sync over WebSocket | `path=wss://...` | JSON only |
| Connect to a realtime sync separately | `rts=...` (combined with `path`/`data`/`local`) | — |

For details and constraints see [uri-format.md](uri-format.md) /
[resource-loading.md](resource-loading.md).

## 4. Relationship to NetworkSyncService

AppLink can **point at / carry** a NetworkSyncService (HTTP/WebSocket
sync protocol) endpoint, but AppLink itself is not the sync protocol.

- `path=ws://...` / `path=wss://...`: receive timetable delivery + location
  sync over WebSocket. The message spec after connection is under
  [../../network-sync-service/en/websocket.md](../../network-sync-service/en/websocket.md).
- `rts=...`: after loading the timetable (`path`/`data`/`local`), connect
  to the separately specified sync server (see
  [resource-loading.md](resource-loading.md#4-realtime-integration-rts--rtk--rtv)).

For the sync protocol itself see
[../../network-sync-service/](../../network-sync-service/README.md).

## 5. Security summary

Because an AppLink can be injected arbitrarily from outside, TRViS
inserts user confirmation before potentially dangerous operations. See
[confirmation gates in resource-loading.md](resource-loading.md#3-confirmation-gates-user-confirmation).
Key points:

- **Remote fetch (`http`/`https`)** prompts the user before opening, and
  re-confirms with the size from a HEAD request.
- Connections judged to be a **private IP on a different network**
  prompt for whether to continue.
- **`local=`** rejects path traversal in two stages (syntactic and
  semantic); it cannot reference outside the on-device timetable folder.
- When **`rts`**'s host differs from the timetable resource's host, it
  confirms before connecting.
- The link itself has no authentication. If you embed confidential data
  in `data=`, anyone who can receive the URL can recover it (`enc`
  encryption is currently `none` only = unsupported).

## 6. Version

The `ver` query is the AppLink format version (default `1.0`, max
supported `1.0`). A link exceeding the supported max is rejected. See
[`ver` in uri-format.md](uri-format.md#5-version-ver).

> Note: internal hosts beginning with `trvis://_test/...` are exclusively
> for UI_TEST-build test infrastructure and are not part of the public
> specification.
