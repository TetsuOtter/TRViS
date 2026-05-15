# TRViS AppLink Docs / AppLink 仕様ドキュメント

Specification of the `trvis://` custom-URL-scheme deep link ("AppLink")
that opens the TRViS app and makes it load a timetable (and optionally
connect to a realtime sync server).

TRViS アプリを起動して時刻表を読み込ませる（必要に応じてリアルタイム
同期サーバーへ接続させる）ための `trvis://` カスタム URL スキーム
ディープリンク（「AppLink」）の仕様です。

| Language / 言語 | Entry point / 入口 |
|---|---|
| 日本語 | [ja/README.md](ja/README.md) |
| English | [en/README.md](en/README.md) |

---

## Scope / 位置づけ

This document set covers **how to invoke the app via a URL** — the URI
grammar, query parameters, resource loading behavior, and platform
registration.

本ドキュメント群は **URL によるアプリ起動の方法** ——URI 文法・クエリ
パラメータ・リソース読み込み挙動・プラットフォーム登録——を扱います。

AppLink can point at, or carry, a NetworkSyncService endpoint
(`path=ws(s)://...` or `rts=...`). The HTTP/WebSocket sync **protocol
itself** is documented separately under
[`../network-sync-service/`](../network-sync-service/README.md); AppLink
only *invokes* it.

AppLink は NetworkSyncService のエンドポイントを指す／内包できます
（`path=ws(s)://...` または `rts=...`）。HTTP/WebSocket 同期
**プロトコル自体** は別途
[`../network-sync-service/`](../network-sync-service/README.md)
にドキュメント化されています。AppLink はそれを*呼び出す*だけです。

## Document structure / ドキュメント構成

| File / ファイル | Topic / 主題 |
|---|---|
| `README.md` | 概要・スキーム要約・目次・セキュリティ要約 / Overview, scheme summary, ToC, security summary |
| `uri-format.md` | URI 文法とクエリパラメータの完全仕様 / Full URI grammar & query parameters |
| `resource-loading.md` | スキーム別の読み込み挙動・確認ダイアログ・履歴・リアルタイム連携 / Per-scheme loading, confirmation gates, history, realtime |
| `platform-registration.md` | iOS / MacCatalyst / Android の登録と起動経路 / Platform registration & invocation pipeline |

## Source of truth / 出典

Derived from the implementation: `TRViS.IO/RequestInfo/AppLinkInfo.cs`
(parsing), `TRViS/ViewModels/AppViewModel.AppLink.cs` (handling),
`TRViS.IO/OpenFile.cs` (resource loading), and the platform manifests.

実装に基づきます: `TRViS.IO/RequestInfo/AppLinkInfo.cs`（解析）、
`TRViS/ViewModels/AppViewModel.AppLink.cs`（処理）、
`TRViS.IO/OpenFile.cs`（リソース読み込み）、各プラットフォームの
マニフェスト。
