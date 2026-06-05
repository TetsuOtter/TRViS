# NetworkSyncService 外部システム連携仕様（日本語）

> 対応プロトコルバージョン: **1.0**
> 対象読者: TRViS と連携する外部サーバーを実装する開発者
> English: [../en/README.md](../en/README.md)

---

## 1. 概要

`NetworkSyncService` は、TRViS（クライアント）が外部システム（サーバー）から
**運行同期データ・時刻表・各種リモートコマンド**を受け取るための仕組みです。
2 種類のトランスポートをサポートします。

| トランスポート | スキーム | 通信モデル | 主な用途 |
|---|---|---|---|
| **HTTP** | `http://` / `https://` | クライアントからのポーリング | 位置・時刻・サービス利用可否のみの最小連携 |
| **WebSocket** | `ws://` / `wss://` | サーバープッシュ（イベント駆動） | 時刻表配信・リモート操作を含むフル連携 |

接続先 URI のスキームによって TRViS が自動的にトランスポートを選択します
（`ws`/`wss` なら WebSocket、それ以外は HTTP）。1 接続につき 1 トランスポート
のみが使われ、HTTP と WebSocket が同一接続上で混在することはありません。

## 2. ドキュメント構成

| ファイル | 内容 |
|---|---|
| [common-data-model.md](common-data-model.md) | **最初に読む**。両トランスポート共通のデータモデル（`SyncedData`、`Time_ms`、`CanStart`、`Location_m` と緯度経度フォールバック） |
| [http.md](http.md) | HTTP プロトコル詳細（プリフライト・ポーリング・リクエスト/レスポンス・失敗時の扱い） |
| [websocket.md](websocket.md) | WebSocket プロトコル詳細（接続・フレーミング・メッセージ判別・キープアライブ・再接続） |
| [server-to-client-messages.md](server-to-client-messages.md) | サーバー→クライアントの全メッセージ仕様（全 `MessageType` のフィールド定義・パース規則・例） |
| [client-to-server-messages.md](client-to-server-messages.md) | クライアント→サーバーのメッセージ仕様（ID 更新・各種要求） |
| [timetable.md](timetable.md) | 時刻表配信の詳細（スコープ決定・キャッシュ再構築・位置情報リセット） |

推奨読了順: **common-data-model → 実装するトランスポート（http または websocket）
→ server-to-client-messages → client-to-server-messages → timetable**

## 3. 機能対応表

HTTP は WebSocket の **厳密なサブセット** です。HTTP では同期データ
（位置・時刻・サービス利用可否）しか扱えません。時刻表配信やリモートコマンドが
必要な場合は WebSocket を実装してください。

| 機能 | HTTP | WebSocket | 詳細 |
|---|:---:|:---:|---|
| 位置同期（`Location_m`） | ✅ | ✅ | [common-data-model](common-data-model.md) |
| 時刻同期（`Time_ms`） | ✅ | ✅ | [common-data-model](common-data-model.md) |
| サービス利用可否／自動運行開始（`CanStart`） | ✅ ※3 | ✅ | [common-data-model](common-data-model.md#4-canstart-の意味) |
| 緯度経度フォールバック | ❌ ※1 | ✅ | [common-data-model](common-data-model.md) |
| 時刻表配信（Timetable） | ❌ | ✅ | [timetable](timetable.md) |
| サーバー情報（ServerInfo） | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| ダイヤ情報（DiagramInfo） | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| 列車選択（SelectTrain） | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| 運行操作（OperationCommand） | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| ヘッダ色変更（HeaderColor） | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| 通告（Notification） | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| 時刻表示書式（TimeFormat） | ❌ | ✅ | [server-to-client-messages](server-to-client-messages.md) |
| クライアント→サーバーの ID 通知 | ✅ ※2 | ✅ | 各トランスポート文書 |

- ※1: HTTP クライアントは応答 JSON に緯度経度が含まれていても **無視** します
  （`Location_m` / `Time_ms` / `CanStart` の 3 フィールドのみ解釈）。
- ※2: HTTP はクエリパラメータ、WebSocket は JSON メッセージで通知します。
- ※3: `CanStart` は `CanUseService` と同値。**自動運行開始は WebSocket
  接続時のみ**で、HTTP では `CanStart` が `true` でも自動的に運行は
  開始しません（[common-data-model §4](common-data-model.md#4-canstart-の意味)）。

## 4. セキュリティ（重要）

**このプロトコル自体には認証・認可の仕組みがありません。** ワイヤ上の
メッセージは匿名で、接続元の検証も行いません。実運用では以下を実装側で
別途用意してください。

- TLS（`https://` / `wss://`）による通信路の暗号化
- リバースプロキシや WAF によるアクセス制御
- URI のクエリ・パス、TLS クライアント証明書等を用いた認可
  （TRViS は接続先 URI をそのまま使用するため、URI にトークンを埋め込む
  運用が可能。HTTP ではクエリ、WebSocket でも `ws(s)://host/path?token=...`
  の形で URI に含められる）

TRViS は配信されたコマンド（`SelectTrain` / `OperationCommand` /
`HeaderColor` 等）を信頼して実行します。サーバーになりすました第三者が
これらを送れる状態は重大なリスクであるため、トランスポート保護は必須です。

## 5. プロトコルバージョン

`ServerInfo` メッセージの `ProtocolVersion` フィールドが、プロトコル
互換性を示す唯一のハンドシェイク的シグナルです。本ドキュメントが対象と
するプロトコルは **`"1.0"`** です。クライアントは現状この値で接続を
拒否することはありませんが、将来の互換性判定のために正しい値を返すことを
推奨します。
