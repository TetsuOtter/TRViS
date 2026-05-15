# サーバー → クライアント メッセージ仕様（日本語）

> [← 目次に戻る](README.md) ／ 前提: [common-data-model.md](common-data-model.md) ／
> [websocket.md](websocket.md)
> English: [../en/server-to-client-messages.md](../en/server-to-client-messages.md)

**WebSocket 専用。** サーバーからクライアントへプッシュできる全
メッセージの詳細仕様です。各メッセージは UTF-8 テキストフレームの
JSON オブジェクトで、必ず `MessageType` フィールド（正確なケース）を
持ちます。未知／欠落の `MessageType` は無視されます。

| `MessageType` | 用途 | 節 |
|---|---|---|
| `SyncedData` | 位置・時刻・発車可否の同期 | [§1](#1-synceddata) |
| `Timetable` | 時刻表配信 | [§2](#2-timetable) |
| `ServerInfo` | サーバー情報 | [§3](#3-serverinfo) |
| `DiagramInfo` | ダイヤ情報 | [§4](#4-diagraminfo) |
| `SelectTrain` | 列車選択指示 | [§5](#5-selecttrain) |
| `OperationCommand` | 運行操作指示 | [§6](#6-operationcommand) |
| `HeaderColor` | ヘッダ色変更 | [§7](#7-headercolor) |
| `Notification` | 通告 | [§8](#8-notification) |
| `TimeFormat` | 時刻表示書式 | [§9](#9-timeformat) |

> 表記規約: 「必須」はサーバーが意味のある動作をさせるために事実上
> 必要なフィールド。「任意」は省略可能。型不一致はおおむね「無視
> （デフォルト値）」として扱われ、例外にはなりません。

---

## 1. SyncedData

最も基本的なメッセージ。位置・時刻・発車可否をプッシュします。
WebSocket では受信のたびに即座に処理されます（バッファリングなし）。

```jsonc
{
  "MessageType": "SyncedData",
  "Location_m": 1234.5,        // number | null。null は距離未確定
  "Time_ms": 43200000,         // integer。その日の0時からのミリ秒
  "CanStart": true,            // boolean。省略時 true
  "Latitude_deg": 35.681236,   // number | null（任意）
  "Longitude_deg": 139.767125, // number | null（任意）
  "Accuracy_m": 5.0            // number | null（任意）
}
```

| フィールド | 型 | 既定（欠落時） | 説明 |
|---|---|---|---|
| `Location_m` | number \| null | `null`（NaN） | 始点からの距離 [m]。`null`/型不正で NaN。 |
| `Time_ms` | integer | `0` | その日の 0 時からのミリ秒。 |
| `CanStart` | boolean | **`true`** | 発車可否。 |
| `Latitude_deg` | number | `null` | 緯度。number 型でなければ無効。 |
| `Longitude_deg` | number | `null` | 経度。number 型でなければ無効。 |
| `Accuracy_m` | number | `null` | 測位精度 [m]。 |

フィールドの意味・駅判定への影響は
[共通データモデル](common-data-model.md)を参照してください。

## 2. Timetable

時刻表データを配信します。`Data` に
[TRViS JSON 形式](https://github.com/TetsuOtter/TRViS/wiki/JSON%E5%BD%A2%E5%BC%8F%E3%81%AE%E3%83%87%E3%83%BC%E3%82%BF%E3%83%99%E3%83%BC%E3%82%B9)
の時刻表本体を **生 JSON（文字列でなくオブジェクト／配列）** として
埋め込みます。

```jsonc
{
  "MessageType": "Timetable",
  "WorkGroupId": "wg-1",   // 任意（スコープ決定に使用）
  "WorkId": "w-1",         // 任意（スコープ決定に使用）
  "TrainId": "t-1",        // 任意（スコープ決定に使用）
  "Data": { /* または [...] : TRViS JSON 形式の時刻表本体 */ }
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `WorkGroupId` | string | 任意。対象 WorkGroup。 |
| `WorkId` | string | 任意。対象 Work。 |
| `TrainId` | string | 任意。対象 Train。 |
| `Data` | object \| array | 時刻表本体（TRViS JSON 形式の生 JSON）。 |

- どの ID が含まれるかでスコープ（All / WorkGroup / Work / Train）が
  決まり、`Data` の型もスコープに対応します。
- スコープ決定の規則、キャッシュ再構築の挙動、All スコープでの位置情報
  リセットなどの詳細は **[timetable.md](timetable.md)** を参照してください。
- `Data` の中身（時刻表本体の構造）は本ドキュメント群の範囲外です
  （上記 TRViS JSON 形式 Wiki を参照）。

## 3. ServerInfo

サーバー自身の情報。クライアントの `RequestServerInfo`
（[client-to-server-messages.md](client-to-server-messages.md)）への応答
としても、サーバー主導のブロードキャストとしても送れます。

```jsonc
{
  "MessageType": "ServerInfo",
  "Name": "My Sync Server",     // string | null
  "Admin": "admin@example.com", // string | null
  "Version": "1.2.3",           // string | null
  "ProtocolVersion": "1.0"      // string | null
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `Name` | string | サーバー名。 |
| `Admin` | string | 管理者・連絡先。 |
| `Version` | string | サーバー実装バージョン。 |
| `ProtocolVersion` | string | 対応プロトコルバージョン。現行は `"1.0"`。 |

各フィールドは `null` または欠落で「未設定」扱い。`ProtocolVersion` は
プロトコル互換性を示す唯一のシグナルなので、正しい値を返すことを推奨します。

## 4. DiagramInfo

WorkGroup の上位概念である「ダイヤ」の情報。`RequestDiagramInfo` への
応答、またはサーバー主導ブロードキャストで送れます。

```jsonc
{
  "MessageType": "DiagramInfo",
  "DiagramId": "d-1",                  // string | null（クライアント側 Id）
  "Name": "平日ダイヤ",                // string | null
  "Description": "2024年3月改正",      // string | null
  "WorkGroupIds": ["wg-1", "wg-2"]     // string[] | null
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `DiagramId` | string | ダイヤ識別子（クライアント内部では `Id`）。 |
| `Name` | string | ダイヤ名称。 |
| `Description` | string | 説明・補足。 |
| `WorkGroupIds` | string[] | このダイヤに含まれる WorkGroup ID 一覧。文字列要素のみ採用。 |

> 送出時のキーは `DiagramId` です（クライアント内部表現の `Id` に
> マッピングされます）。`WorkGroupIds` は JSON 配列で、文字列以外の
> 要素は無視されます。

## 5. SelectTrain

クライアントに特定の列車を選択させる指示。`null`／省略した階層は
変更しません（将来拡張のための部分指定に対応）。

```jsonc
{
  "MessageType": "SelectTrain",
  "WorkGroupId": "wg-1",  // string | null
  "WorkId": "w-1",        // string | null
  "TrainId": "t-1"        // string | null
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `WorkGroupId` | string | 任意。選択する WorkGroup。 |
| `WorkId` | string | 任意。選択する Work。 |
| `TrainId` | string | 任意。選択する Train。 |

各フィールドは **JSON 文字列型** のときのみ採用されます（数値等は無視）。

## 6. OperationCommand

運行に関する操作指示。

```jsonc
{
  "MessageType": "OperationCommand",
  "Action": "StartOperation"   // 必須。下表のいずれか（大文字小文字無視）
}
```

| `Action` の値 | 意味 |
|---|---|
| `StartOperation` | 運行開始（位置情報サービスを有効化し運行モードへ） |
| `EndOperation` | 運行終了 |
| `EnableLocationService` | 位置情報サービスを有効化 |
| `DisableLocationService` | 位置情報サービスを無効化 |

- `Action` は **必須**。欠落・空文字の場合、このメッセージは無視され
  ます。
- 値は大文字小文字を区別せず解釈されます（例: `startoperation` も可）。
- 上表にない未知の値は無視されます。

## 7. HeaderColor

タイトルバー（ヘッダ）の色変更要求。

```jsonc
{
  "MessageType": "HeaderColor",
  "ResetToDefault": false,   // boolean。true なら端末既定色に戻す
  "Color_RGB": 16711680      // integer (0xRRGGBB)。この例は赤 0xFF0000
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `ResetToDefault` | boolean | JSON の `true` のときのみ「既定へ戻す」。それ以外（`false`/欠落）は false 扱い。 |
| `Color_RGB` | integer | `0xRRGGBB` 形式の整数。`ResetToDefault=true` のときは無視。 |

- `ResetToDefault` は厳密に JSON `true` のときのみ真。
- `Color_RGB` は JSON 数値かつ 32bit 整数として読めるときのみ採用。
  `16711680`（= `0xFF0000`）は赤。

## 8. Notification

通告（任意のお知らせ）。受信イベントとして通知されます（画面表示の
詳細はクライアント実装に依存）。

```jsonc
{
  "MessageType": "Notification",
  "Id": "n-001",                          // string | null
  "Title": "運転見合わせ",                // string | null
  "Body": "強風のため…",                  // string | null
  "Priority": 1,                          // integer（0=通常,1=重要 等。サーバ任意）
  "IssuedAt": "2024-03-01T09:00:00+09:00" // string (ISO 8601) | null
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `Id` | string | 通告識別子。 |
| `Title` | string | 見出し。 |
| `Body` | string | 本文。 |
| `Priority` | integer | 重要度。JSON 数値かつ 32bit 整数のときのみ採用、既定 `0`。意味づけはサーバー任意。 |
| `IssuedAt` | string | 発行時刻。**ISO 8601**（往復可能形式、例 `2024-03-01T09:00:00+09:00`）。解釈できない場合は未設定。 |

## 9. TimeFormat

タイトルバーの時刻表示書式の指定。

```jsonc
{
  "MessageType": "TimeFormat",
  "Format": "HH:mm:ss"   // string | null。null/省略で端末既定にリセット
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `Format` | string | 例: `"HH:mm:ss"` / `"HH:mm"`。`null` または省略時は端末既定にリセット。 |

書式文字列の解釈はクライアント側の時刻フォーマッタに準じます。

---

## 付録: パース挙動の要点

外部実装者が誤りやすい点のまとめです。

- **封筒キーは大文字小文字を区別**します（`MessageType`, `Location_m`
  等）。`Timetable` の `Data` 内（時刻表本体）のみ大文字小文字非区別。
- **型が違うフィールドはおおむね「無視＝デフォルト」**になり、例外は
  発生しません。意図した値を確実に届けるには正しい JSON 型で送ること。
- `SyncedData.CanStart` は **省略時 `true`**。発車抑止は明示的に `false`。
- `Latitude_deg`/`Longitude_deg`/`Accuracy_m`/`Color_RGB`/`Priority` は
  **JSON number 型必須**（文字列は無効）。
- `SelectTrain` の各 ID は **JSON string 型必須**。
- `OperationCommand.Action` は**必須**かつ既知の値のみ有効（大小無視）。
- `Notification.IssuedAt` は **ISO 8601** のみ。
- 未知の `MessageType`・`MessageType` 欠落・不正 JSON は **黙って無視**。
