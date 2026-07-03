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
| `SyncedData` | 位置・時刻・サービス利用可否（自動運行開始）の同期 | [§1](#1-synceddata) |
| `Timetable` | 時刻表配信 | [§2](#2-timetable) |
| `ServerInfo` | サーバー情報 | [§3](#3-serverinfo) |
| `DiagramInfo` | ダイヤ情報 | [§4](#4-diagraminfo) |
| `SelectTrain` | 列車選択指示 | [§5](#5-selecttrain) |
| `OperationCommand` | 運行操作指示 | [§6](#6-operationcommand) |
| `HeaderColor` | ヘッダ色変更 | [§7](#7-headercolor) |
| `Notification` | 通告 | [§8](#8-notification) |
| `TimeFormat` | 時刻表示書式 | [§9](#9-timeformat) |
| `NavigateToHome` | ホーム画面へ遷移 | [§10](#10-navigatetohome) |
| `OpenTimetable` | 指定列車の時刻表ビューを開く | [§11](#11-opentimetable) |
| `SearchTrainResponse` | 列車検索要求への結果応答 | [§12](#12-searchtrainresponse) |

> 表記規約: 「必須」はサーバーが意味のある動作をさせるために事実上
> 必要なフィールド。「任意」は省略可能。型不一致はおおむね「無視
> （デフォルト値）」として扱われ、例外にはなりません。

---

## 1. SyncedData

最も基本的なメッセージ。位置・時刻・サービス利用可否をプッシュします。
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
| `CanStart` | boolean | **`true`** | サービス利用可否／自動運行開始の許可（`CanUseService` と同値。WS では `true` で自動運行開始）。 |
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
  "ProtocolVersion": "1.1",     // string | null
  "Features": ["TrainSearch"]   // string[] | null。任意。省略/null で拡張機能なし。
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `Name` | string | サーバー名。 |
| `Admin` | string | 管理者・連絡先。 |
| `Version` | string | サーバー実装バージョン。 |
| `ProtocolVersion` | string | 対応プロトコルバージョン。現行は `"1.1"`。 |
| `Features` | string[] | 任意。サーバーが対応する機能 ID 文字列。文字列要素のみ採用し、文字列以外は無視。欠落／`null` は拡張機能を提供しないことを意味する。 |

各フィールドは `null` または欠落で「未設定」扱い。`ProtocolVersion` は
プロトコル互換性を示すハンドシェイク的シグナルであり（任意機能は下記の
`Features` で別途ネゴシエーションされます）、正しい値を返すことを推奨します。

**機能ネゴシエーション。** `Features` はバージョン番号とは独立に
ネゴシエーションされる任意機能を広告します。現在既知の機能 ID は
次のとおりです。

| 機能 ID | 意味 |
|---|---|
| `"TrainSearch"` | サーバーが列車検索の要求/応答ペア（[`SearchTrain`](client-to-server-messages.md#4-searchtrain) → [`SearchTrainResponse`](#12-searchtrainresponse)）および後続の [`RequestTrainTimetable`](client-to-server-messages.md#5-requesttraintimetable) に対応する。 |

クライアントは WebSocket 接続確立直後に
[`RequestServerInfo`](client-to-server-messages.md#2-requestserverinfo)
を自動送信するため、追加操作なしで `Features` を取得でき、これをもとに
列車検索 UI を表示するかどうかを判断します。

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
  "OrderNumber": "指令003号",             // string | null（指令番号）
  "Title": "運転見合わせ",                // string | null
  "Body": "強風のため…",                  // string | null（BBCode 可）
  "Priority": 1,                          // integer（0=通常,1=重要 等。サーバ任意）
  "IssuedAt": "2024-03-01T09:00:00+09:00",// string (ISO 8601、TZ オフセット任意) | null
  "Receiver": "○○列車 乗務員",            // string | null（受信者）
  "Sender": "指令所",                     // string | null（指令者）
  "IconText": "指",                       // string | null（アイコン文字。IconImageBase64 未指定時のみ使用）
  "IconColor_RGB": "#C62828",              // integer (0xRRGGBB) | string ("#RRGGBB") | 省略（アイコン背景色。既定色あり）
  "IconImageBase64": null,                // string | null（アイコン画像。指定時は IconText より優先）
  "Acknowledged": false                   // boolean（省略可）。受領済みなら true
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `Id` | string | 通告識別子。受領（[`AcknowledgeNotification`](client-to-server-messages.md#4-acknowledgenotification)）の対象キーになる。 |
| `OrderNumber` | string | 指令番号。表示のみに用いる（サーバー・現場運用側の管理番号）。 |
| `Title` | string | 見出し。 |
| `Body` | string | 本文。**BBCode**（`[b]…[/b]` 等）を使用できる。表示側の解釈はクライアント実装に依存。 |
| `Priority` | integer | 重要度。JSON 数値かつ 32bit 整数のときのみ採用、既定 `0`。意味づけはサーバー任意。 |
| `IssuedAt` | string | 発行時刻。**ISO 8601**（往復可能形式）。TZ オフセット（`Z` または `+HH:mm`/`-HH:mm`）の有無で解釈が変わる（下記参照）。解釈できない場合は未設定。 |
| `Receiver` | string | 受信者。表示のみに用いる。 |
| `Sender` | string | 指令者（発信者）。表示のみに用いる。 |
| `IconText` | string | アイコンとして表示する文字（1〜2 文字程度を想定）。`IconImageBase64` が指定されている場合は使用されない。 |
| `IconColor_RGB` | integer \| string | `IconText` の背景色。**JSON 数値**（`0xRRGGBB` の 10 進表記、32bit 整数として読めるもの）または **`"#RRGGBB"` 形式の文字列**（先頭 `#` 必須、大文字小文字不問）のどちらでも指定できる。いずれの形式でも解釈できない場合は未設定（既定色）。未指定時はクライアント既定色。 |
| `IconImageBase64` | string | アイコン画像の Base64 エンコードされたバイナリ（`data:image/png;base64,...` のような data URI プレフィックスを含んでいてもよい）。指定されている場合、`IconText`/`IconColor_RGB` より優先して表示する。 |
| `Acknowledged` | boolean | 任意。当該クライアントが既にこの通告を受領済みかをサーバーが示す。JSON の `true` のときのみ受領済み扱いで、それ以外（`false`/欠落）は未受領。再接続後などに通告を再配信する際、受領済みのものを既読として渡すために使う（クライアントは既読の通告を再度ポップアップ表示しない）。 |

**`IssuedAt` の TZ 有無による表示の違い:**
- TZ オフセットあり（例 `2024-03-01T09:00:00+09:00` や `...Z`）: クライアントは**端末の現在の TZ を考慮**して変換した時刻を表示する。
- TZ オフセットなし（例 `2024-03-01T09:00:00`）: クライアントは**その時刻をそのまま**（TZ 変換せずに）表示する。
- 日時区切りの `T` を含まない文字列（例 `2024-03-01` のような日付のみ、`2024-03-01 09:00:00` のような空白区切り）は **ISO 8601 の日時形式ではない**ため、常に TZ 指定なし扱い（そのまま表示）になる。

受領（クライアント → サーバー）については
[`AcknowledgeNotification`](client-to-server-messages.md#4-acknowledgenotification) を参照。

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

## 10. NavigateToHome

クライアントをホーム画面（スタート画面）へ遷移させる指示。
追加フィールドは不要で、`MessageType` のみのペイロードとなります。

```jsonc
{
  "MessageType": "NavigateToHome"
}
```

フィールドは `MessageType` のみ。受信するとクライアントはホーム画面へ
即座に遷移します（実行中の操作状態や列車選択はサーバー主導の他のコマンドで
別途制御してください）。

---

## 11. OpenTimetable

指定の列車を選択し、D-TAC 画面の「時刻表」タブを直接開きます。
受信するとクライアントは以下を順番に実行します。

1. 列車選択を適用（WorkGroupId / WorkId / TrainId） — [`SelectTrain`](#5-selecttrain) と同じルール。
2. D-TAC 画面が表示されていなければ遷移する。
3. 時刻表（VerticalView）タブへ切り替える。

```jsonc
{
  "MessageType": "OpenTimetable",
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
`null` または省略した場合、その階層の選択は変更されません。

---

## 12. SearchTrainResponse

クライアントの [`SearchTrain`](client-to-server-messages.md#4-searchtrain)
要求への応答。サーバーが `TrainSearch` [機能](#3-serverinfo)を広告して
いる場合のみ提供されます。このメッセージは要求の `RequestId` を
エコーバックし、クライアントは送信した要求と応答を対応付けます。

```jsonc
{
  "MessageType": "SearchTrainResponse",
  "RequestId": "3f2a...unique...",   // SearchTrain の RequestId をエコー（対応付けに必須）
  "Results": [
    {
      "WorkGroupId": "wg-1",
      "WorkId": "w-1",
      "TrainId": "t-1",
      "TrainNumber": "1234",
      "WorkName": "1行路",
      "Direction": 1,                 // integer | null。-1 = Inbound, 1 = Outbound
      "StartStationName": "東京",
      "StartTime": "09:00",
      "EndStationName": "大阪",
      "EndTime": "12:30"
    }
    // ... 0 件以上の候補。同じ列車番号でも複数の候補
    //     （異なる行路／列車）が返ることがある。
  ]
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `RequestId` | string | [`SearchTrain.RequestId`](client-to-server-messages.md#4-searchtrain) のエコー。必須 — クライアントはこの値で応答を対応付ける。 |
| `Results` | object[] | 常に存在。該当候補の配列。**空配列は「該当列車なし」を意味**する（タイムアウトではなく成功応答）。 |

`Results` の各要素は、(a) 候補一覧の表示と (b) 確認ダイアログの表示に
使われるサマリです。時刻表本体の行は **含まれません** — それは
[`RequestTrainTimetable`](client-to-server-messages.md#5-requesttraintimetable)
で別途取得します。

| 結果フィールド | 型 | 説明 |
|---|---|---|
| `WorkGroupId` | string | 列車の取得・表示に必要な ID。 |
| `WorkId` | string | 列車の取得・表示に必要な ID。 |
| `TrainId` | string | 列車の取得・表示に必要な ID。 |
| `TrainNumber` | string | 列車番号。 |
| `WorkName` | string | この列車が属する行路の名称。 |
| `Direction` | integer \| null | `-1` = Inbound, `1` = Outbound。 |
| `StartStationName` | string | 始発駅名。 |
| `StartTime` | string | 始発時刻の表示用文字列（例 `"09:00"`）。 |
| `EndStationName` | string | 終着駅名。 |
| `EndTime` | string | 終着時刻の表示用文字列（例 `"12:30"`）。 |

- `Results` は **常に存在**し、空配列は正常な「該当なし」応答です。
  `TrainSearch` を広告するサーバーは、たとえ 0 件でも **必ず応答**
  しなければなりません。そうすることでクライアントは「該当なし」と
  「無応答／応答失敗」を区別できます（何も届かないとクライアントは
  10 秒でタイムアウトしエラーを報告します）。
- 一致判定はリクエストの `MatchMode`
  （[`SearchTrain`](client-to-server-messages.md#4-searchtrain) 参照）
  — `Prefix`（既定）/ `Contains` / `Exact` — によって決まり、サーバー任意ではありません。

---

## 付録: パース挙動の要点

外部実装者が誤りやすい点のまとめです。

- **封筒キーは大文字小文字を区別**します（`MessageType`, `Location_m`
  等）。`Timetable` の `Data` 内（時刻表本体）のみ大文字小文字非区別。
- **型が違うフィールドはおおむね「無視＝デフォルト」**になり、例外は
  発生しません。意図した値を確実に届けるには正しい JSON 型で送ること。
- `SyncedData.CanStart` は **省略時 `true`**。意味は「サービス利用可否／
  自動運行開始の許可」で **WS では `true` で自動運行開始**。意図せず運行
  させたくない場合は明示的に `false`（[common-data-model §4](common-data-model.md#4-canstart-の意味)）。
- `Latitude_deg`/`Longitude_deg`/`Accuracy_m`/`Color_RGB`/`Priority` は
  **JSON number 型必須**（文字列は無効）。
- `SelectTrain` の各 ID は **JSON string 型必須**。
- `OperationCommand.Action` は**必須**かつ既知の値のみ有効（大小無視）。
- `Notification.IssuedAt` は **ISO 8601** のみ。TZ オフセットの有無で表示の変換有無が変わる（[§8](#8-notification) 参照）。
- `ServerInfo.Features` は **JSON 配列**で、文字列要素のみ採用し文字列
  以外は無視（欠落／`null` は拡張機能なし）。
- 未知の `MessageType`・`MessageType` 欠落・不正 JSON は **黙って無視**。
