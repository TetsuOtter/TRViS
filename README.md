# TRViS

様々な時刻表を色々なフォーマットで表示できるアプリケーションです。

**業務での使用は想定していません。趣味の範囲内で利用いただくことをおすすめします。**

MITライセンスに基づき自由に使用、改造等行うことができます。
なお、使用、改造その他本プロダクトに関連して生じた一切の損害について、作者の故意の場合を除き責を負いませんのでご了承ください。

[TestFlightにてベータ版を配信中](https://testflight.apple.com/join/yYBaAdqX)

## 使い方

アプリを起動すると、画面中央に「Work Group」「Work」「Train」の3つのリストと、右上に「Select Database File」というボタンが見えます。

基本は、データベースファイルを読み込み、リストからWorkGroup、Work、Trainを順に選んでいく形になります。

なお、初期状態ではサンプルデータが読み込まれています。

データを選択したら、左上のメニューボタンを押下し、メニューを開きます。

メニューを開くと、ページ一覧が出てくるので、そこから目的のページを選択し、移動してください。

## 注意事項

- iPad miniにて縦向きで動作させることを想定して作成しています。それ以外の環境では、レイアウトが崩れる場合があります。
- iPhone等のスマートフォンでも起動できるようにしていますが、端末の画面が小さい影響で、スクロールしないとコンテンツを表示できない場合があります。
- スマートフォンで使用する場合、端末を横向きにして使用することをおすすめします。

## ライセンス / 開発への貢献

MITライセンスにて公開しております。

改良/改造ともに大歓迎です。
また、Issue / PRとも、お気軽にお寄せください。

Issueを立てるほどでも…というような場合は、Discussion機能をご利用ください。

## 用語解説

### TRViS

アプリ名です。

### WorkGroup

一人が担当する列車(Train)は、1勤務で複数存在します。そして、勤務先には複数の乗務員が在籍し、乗務していることでしょう。

1DB = 1乗務員区でも問題ありませんが、1ファイルで複数の乗務員区、あるいは「ダイヤ改正前」と「ダイヤ改正後」を同じファイルに入れてしまいたい…という需要を考慮し、仕業(Work)は仕業グループ(WorkGroup)にまとめられています。

### Work

会社によって呼び方は違うでしょうが、「仕業」といえば伝わるでしょうか。

乗務員が一度の勤務で乗務する列車のまとまりです。

### Train

言わずもがな、列車です。

## データベース構造

将来的にはデータベース作成ツールを組み込むつもりですが、現在は未実装です。
ただし、本アプリはSQLite3をDBエンジンとして使用しており、フリーソフトを用いて簡単にデータベースを作成することができます。

以下に、データベース構造を示します。
なお、DB作成に使用できるSQLファイルを用意しているので、DB作成時は[こちら](https://github.com/TetsuOtter/TRViS/blob/main/TRViS.IO.Tests/Resources/CreateTables.sql)をご活用ください。

データベースは、以下のような6テーブルから構成されます。

|Table Name|Description|
|---:|:---|
|`work_group`|仕業グループを記録する|
|`work`|仕業データに関する情報を記録する|
|`train_data`|列車データを記録する|
|`station`|駅データを記録する|
|`station_track`|各駅の番線情報を記録する|
|`timetable_row`|各列車の各駅到着時刻等を記録する|

### work_groupテーブル

https://github.com/TetsuOtter/TRViS/blob/97de680106a7219d22753f4174b6167342e1f700/TRViS.IO.Tests/Resources/CreateTables.sql#L1-L5

|Column|Type|Description|
|---:|:---:|:---|
|`id`|INTEGER NOT NULL|WorkGroupの一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|
|`name`|TEXT NOT NULL|WorkGroupの名前|

### workテーブル

https://github.com/TetsuOtter/TRViS/blob/97de680106a7219d22753f4174b6167342e1f700/TRViS.IO.Tests/Resources/CreateTables.sql#L7-L14

|Column|Type|Description|
|---:|:---:|:---|
|`id`|INTEGER NOT NULL|WorkGroupの一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|
|`work_group_id`|INTEGER NOT NULL|関連づけるWorkGroupの`id` (`work_group`で作成したWorkGroupのIDを指定する)|
|`name`|TEXT NOT NULL|WorkGroupの名前|

### train_dataテーブル

https://github.com/TetsuOtter/TRViS/blob/a02427bd35a93f4f64046685b68533f858809d7b/TRViS.IO.Tests/Resources/CreateTables.sql#L16-L33

(表は後ほど追加します)

### stationテーブル

https://github.com/TetsuOtter/TRViS/blob/a02427bd35a93f4f64046685b68533f858809d7b/TRViS.IO.Tests/Resources/CreateTables.sql#L35-L42

(表は後ほど追加します)

### station_trackテーブル

https://github.com/TetsuOtter/TRViS/blob/a02427bd35a93f4f64046685b68533f858809d7b/TRViS.IO.Tests/Resources/CreateTables.sql#L44-L50

(表は後ほど追加します)

### timetable_rowテーブル

https://github.com/TetsuOtter/TRViS/blob/a02427bd35a93f4f64046685b68533f858809d7b/TRViS.IO.Tests/Resources/CreateTables.sql#L52-L79

(表は後ほど追加します)
