# NetworkSyncService Integration Docs / 外部システム連携ドキュメント

External-system integration specification for the TRViS `NetworkSyncService`
(HTTP / WebSocket).

TRViS の `NetworkSyncService`（HTTP / WebSocket）に対する外部システム
連携仕様です。TRViS と連携するサーバーを実装する開発者向けです。

| Language / 言語 | Document / ドキュメント |
|---|---|
| 日本語 | [ja.md](ja.md) |
| English | [en.md](en.md) |

---

## What is this? / これは何？

`NetworkSyncService` lets TRViS (the client) receive operation-sync data,
timetables, and remote commands from an external server. It supports two
transports:

`NetworkSyncService` は、TRViS（クライアント）が外部サーバーから運行同期
データ・時刻表・リモートコマンドを受け取る仕組みです。2 つのトランス
ポートをサポートします。

- **HTTP** (`http://` / `https://`) — client polling; sync data only.
  クライアントからのポーリング。同期データのみ。
- **WebSocket** (`ws://` / `wss://`) — server push; full feature set.
  サーバープッシュ。フル機能。

TRViS picks the transport from the URI scheme automatically.
TRViS は URI スキームからトランスポートを自動選択します。

## Source of truth / 出典

This documentation is derived from the `TRViS.NetworkSyncService`
implementation and the `TRViS.ReferenceServer` reference server.
The timetable body inside `Timetable` messages uses the
[TRViS JSON format](https://github.com/TetsuOtter/TRViS/wiki/JSON%E5%BD%A2%E5%BC%8F%E3%81%AE%E3%83%87%E3%83%BC%E3%82%BF%E3%83%99%E3%83%BC%E3%82%B9)
(documented separately) and is out of scope here — this document covers
the **transport**, not the timetable schema.

本ドキュメントは `TRViS.NetworkSyncService` 実装および
`TRViS.ReferenceServer`（リファレンスサーバー）に基づきます。
`Timetable` メッセージ内の時刻表本体は
[TRViS JSON 形式](https://github.com/TetsuOtter/TRViS/wiki/JSON%E5%BD%A2%E5%BC%8F%E3%81%AE%E3%83%87%E3%83%BC%E3%82%BF%E3%83%99%E3%83%BC%E3%82%B9)
（別途ドキュメント化）に従い、本書の範囲外です。本書は時刻表スキーマ
ではなく**トランスポート**を扱います。
