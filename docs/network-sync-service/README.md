# NetworkSyncService Integration Docs / 外部システム連携ドキュメント

External-system integration specification for the TRViS `NetworkSyncService`
(HTTP / WebSocket).

TRViS の `NetworkSyncService`（HTTP / WebSocket）に対する外部システム
連携仕様です。TRViS と連携するサーバーを実装する開発者向けです。

| Language / 言語 | Entry point / 入口 |
|---|---|
| 日本語 | [ja/README.md](ja/README.md) |
| English | [en/README.md](en/README.md) |

---

## Document structure / ドキュメント構成

各言語フォルダは以下のファイルに分割されています（内容は対応）。

| File / ファイル | Topic / 主題 |
|---|---|
| `README.md` | 概要・機能対応表・セキュリティ・目次 / Overview, capability matrix, security, ToC |
| `common-data-model.md` | 共通データモデル（HTTP/WS 共通） / Common data model |
| `http.md` | HTTP プロトコル詳細 / HTTP protocol |
| `websocket.md` | WebSocket プロトコル詳細 / WebSocket protocol |
| `server-to-client-messages.md` | サーバー→クライアント 全メッセージ仕様 / Server→client message catalog |
| `client-to-server-messages.md` | クライアント→サーバー メッセージ仕様 / Client→server message catalog |
| `timetable.md` | 時刻表配信の詳細（スコープ・キャッシュ） / Timetable delivery deep-dive |

## Source of truth / 出典

This documentation is derived from the `TRViS.NetworkSyncService`
implementation and the `TRViS.ReferenceServer` reference server.
The timetable body inside `Timetable` messages uses the
[TRViS JSON format](https://github.com/TetsuOtter/TRViS/wiki/JSON%E5%BD%A2%E5%BC%8F%E3%81%AE%E3%83%87%E3%83%BC%E3%82%BF%E3%83%99%E3%83%BC%E3%82%B9)
(documented separately) and is out of scope here — this document set
covers the **transport**, not the timetable schema.

本ドキュメントは `TRViS.NetworkSyncService` 実装および
`TRViS.ReferenceServer`（リファレンスサーバー）に基づきます。
`Timetable` メッセージ内の時刻表本体は
[TRViS JSON 形式](https://github.com/TetsuOtter/TRViS/wiki/JSON%E5%BD%A2%E5%BC%8F%E3%81%AE%E3%83%87%E3%83%BC%E3%82%BF%E3%83%99%E3%83%BC%E3%82%B9)
（別途ドキュメント化）に従い、本書の範囲外です。本ドキュメント群は
時刻表スキーマではなく**トランスポート**を扱います。
