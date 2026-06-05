# TRViS AppLink 仕様（日本語）

> 対応 AppLink バージョン: **1.0**
> 対象読者: TRViS を起動する URL（リンク・QR コード等）を生成する開発者
> English: [../en/README.md](../en/README.md)

---

## 1. 概要

**AppLink** は、外部から TRViS アプリを起動し、指定した時刻表を
読み込ませる（さらに任意でリアルタイム同期サーバーへ接続させる）ための
カスタム URL スキームディープリンクです。

URI の基本形:

```
trvis://app/<action>?<query>
```

- スキーム: `trvis://`（OS への登録はこのスキームのみ）
- ホスト: `app`（必須）
- アクション（パス）: `/open/json` または `/open/sqlite`
- クエリ: 読み込むリソースの指定（必須・空不可）

最小例:

```
trvis://app/open/json?path=https://example.com/timetable.json
```

## 2. ドキュメント構成

| ファイル | 内容 |
|---|---|
| [uri-format.md](uri-format.md) | **まず読む**。URI 文法（スキーム/ホスト/アクション）とクエリパラメータの完全仕様、エンコード規則、優先順位、ファイル種別×スキーム対応表、バージョン |
| [resource-loading.md](resource-loading.md) | リソース種別（`file` / `http(s)` / `ws(s)` / インラインデータ）ごとの読み込み挙動、ユーザー確認ダイアログ（確認ゲート）、履歴、リアルタイム連携（`rts`/`rtk`/`rtv`） |
| [platform-registration.md](platform-registration.md) | iOS / MacCatalyst / Android での登録、OS からアプリ内処理までの起動経路、カスタムスキーム限定である点 |

推奨読了順: **uri-format → resource-loading → platform-registration**

## 3. AppLink でできること

| 用途 | 指定方法 | 種別 |
|---|---|---|
| Web 上の JSON 時刻表を開く | `path=https://...`（または `http://`） | JSON のみ |
| Web 上 / ローカルの SQLite を開く | `path=file://...`（SQLite は `file` のみ） | SQLite |
| 端末内の時刻表フォルダのファイルを開く | `local=relative/path` | JSON / SQLite |
| 時刻表データを URL に埋め込んで開く | `data=<URL安全Base64>` | JSON のみ |
| WebSocket で時刻表＋同期を受ける | `path=wss://...` | JSON のみ |
| 時刻表とは別にリアルタイム同期へ接続 | `rts=...`（`path`/`data`/`local` と併用） | — |

詳細・制約は [uri-format.md](uri-format.md) /
[resource-loading.md](resource-loading.md) を参照してください。

## 4. NetworkSyncService との関係

AppLink は NetworkSyncService（HTTP/WebSocket 同期プロトコル）の
エンドポイントを **指す／内包** できますが、AppLink 自体は同期
プロトコルではありません。

- `path=ws://...` / `path=wss://...`: WebSocket 経由で時刻表配信＋
  位置同期を受けます。受信後のメッセージ仕様は
  [../../network-sync-service/ja/websocket.md](../../network-sync-service/ja/websocket.md)
  以下を参照。
- `rts=...`: 時刻表（`path`/`data`/`local`）読み込み後に、別途
  指定した同期サーバーへ接続します（[resource-loading.md](resource-loading.md#4-リアルタイム連携-rts--rtk--rtv) 参照）。

同期プロトコルそのものの仕様は
[../../network-sync-service/](../../network-sync-service/README.md)
を参照してください。

## 5. セキュリティ要約

AppLink は外部から任意に投げ込めるため、TRViS は危険になり得る操作の
前にユーザー確認を挟みます。詳細は
[resource-loading.md の確認ゲート](resource-loading.md#3-確認ゲートユーザー確認)
を参照。要点:

- **リモート取得（`http`/`https`）** は、ファイルを開く前にユーザーへ
  確認し、HEAD でサイズを提示して再確認します。
- **プライベート IP かつ別ネットワーク**と判定される接続先には、
  続行可否を確認します。
- **`local=`** はパストラバーサルを構文・意味の二段で拒否し、端末内の
  時刻表フォルダ外を参照できません。
- **`rts`** の host が時刻表リソースの host と異なる場合、接続前に確認します。
- リンク自体に認証はありません。機密データを `data=` に埋め込む場合、
  URL を受け取れる者は誰でも復元できる点に注意してください
  （`enc` による暗号化は現状 `none` のみ＝未対応）。

## 6. バージョン

`ver` クエリが AppLink フォーマットのバージョンです（既定 `1.0`、
対応最大 `1.0`）。対応最大を超えるとリンクは拒否されます。詳細は
[uri-format.md の `ver`](uri-format.md#5-version-ver)。

> 注: `trvis://_test/...` で始まる内部用ホストは UI_TEST ビルドの
> テスト基盤専用であり、公開仕様には含まれません。
