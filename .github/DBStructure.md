# データベース構造

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

## 注意事項

HTMLで表示内容を書くことができるColumnがありますが、HTMLを利用する場合は**何らかのタグで表示内容全体を囲んで**記録してください。
実装の都合上、文字列の先頭に`<`、加えて末尾に`>`が存在する場合にHTMLで描画するようにしています。

例えば、一部を太文字にしたい場合は、`ABC<b>D</b>EF`ではなく、`<span>ABC<b>D</b>EF</span>`のようにします。

## work_groupテーブル

https://github.com/TetsuOtter/TRViS/blob/eb35688f6f5544b1448f554b692f705f7a715141/TRViS.IO.Tests/Resources/CreateTables.sql#L1-L7

|Column|Type|Description|Version|
|---:|:---:|:---|:---:|
|`id`|INTEGER NOT NULL|WorkGroupテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|0 ~|
|`name`|TEXT NOT NULL|WorkGroupの名前|0 ~|
|`db_version`|INTEGER|データベース構造のバージョン|1 ~|

## workテーブル

https://github.com/TetsuOtter/TRViS/blob/eb35688f6f5544b1448f554b692f705f7a715141/TRViS.IO.Tests/Resources/CreateTables.sql#L9-L24

|Column|Type|Description|Version|
|---:|:---:|:---|:---:|
|`id`|INTEGER NOT NULL|Workテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|0 ~|
|`work_group_id`|INTEGER NOT NULL|関連づけるWorkGroupの`id` (`work_group`で作成したWorkGroupのIDを指定する)|0 ~|
|`name`|TEXT NOT NULL|WorkGroupの名前|0 ~|
|`affect_date`|TEXT NOT NULL|「行路施行日」に表示する日付<br/>`YYYY-MM-DD`形式で指定できます。あるいは、空文字列にすることで、アプリで読み込んだ当日の日付が表示されます|0 ~|
|`affix_content_type`|INTEGER|「行路添付」に表示するデータのデータ形式|1 ~|
|`affix_content`|BLOB|「行路添付」に表示するデータ|1 ~|
|`remarks`|TEXT|行路の注意事項 (HTMLを書くと、任意のスタイルで文字を表示できます)|1 ~|
|`has_e_train_timetable`|INTEGER|「E電時刻表」を持っているか|1 ~|
|`e_train_timetable_content_type`|INTEGER|「E電時刻表」に表示するデータのデータ形式|1 ~|
|`e_train_timetable_content`|BLOB|「E電時刻表」に表示するデータ|1 ~|

## train_dataテーブル

列車ごとの情報を記録するテーブルです。各駅の停車時刻等は`timetable_row`テーブルに記録します。

https://github.com/TetsuOtter/TRViS/blob/eb35688f6f5544b1448f554b692f705f7a715141/TRViS.IO.Tests/Resources/CreateTables.sql#L26-L52

|Column|Type|Description|Version|
|---:|:---:|:---|:---:|
|`id`|INTEGER NOT NULL|TrainDataテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|0 ~|
|`work_id`|INTEGER NOT NULL|関連づけるWorkの`id` (`work_group`で作成したWorkGroupのIDを指定する)|0 ~|
|`train_number`|TEXT NOT NULL|列車番号 (「列番」として表示される)|0 ~|
|`max_speed`|TEXT|「最高速度」として表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`speed_type`|TEXT|「速度種別」として表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`ntc`|TEXT|「けん引定数」として表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`car_count`|INTEGER|編成両数。`NULL`にしても、両数表示は消えません。|0 ~|
|`destination`|TEXT|「`(XXX 行)`」の`XXX`部分に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)<br/>NULLにすると`(XXX 行)`部分が丸ごと表示されなくなります。|0 ~|
|`begin_remarks`|TEXT|「着」の直上あたりに表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`after_remarks`|TEXT|時刻表部分の一番下に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`remarks`|TEXT|「注意事項」に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`before_departure`|TEXT|「発前」に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`train_info`|TEXT|「発前」の上部分に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`direction`|INTEGER NOT NULL|列車の進行方向を表す値<br/>0以上の値を設定することで、駅位置について昇順(`0 to 9`の順)で表示されます。逆に、0未満で降順(`9 to 0`)です。|0 ~|
|`after_arrive`|TEXT|「着後」に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|1 ~|
|`before_departure_on_station_track_col`|TEXT|「発前」に表示される内容のうち、「着発線」と同じ列に表示されるもの|1 ~|
|`after_arrive_on_station_track_col`|TEXT|「着後」に表示される内容のうち、「着発線」と同じ列に表示されるもの|1 ~|
|`day_count`|INTEGER|仕業の初日からの経過日数 (初日なら`NULL`または`0`、明けなら`1`)|1 ~|
|`is_ride_on_moving`|INTEGER|便乗かどうか (便乗なら`1`、そうでないなら`0`)|1 ~|
|`color_id`|INTEGER|E電時刻表で使用する、線の色ID|1 ~|

## stationテーブル

各駅の情報を記録するテーブルです。駅の情報はWorkGroupごとに分けることができます。

https://github.com/TetsuOtter/TRViS/blob/eb35688f6f5544b1448f554b692f705f7a715141/TRViS.IO.Tests/Resources/CreateTables.sql#L54-L68

|Column|Type|Description|Version|
|---:|:---:|:---|:---:|
|`id`|INTEGER NOT NULL|Stationテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|0 ~|
|`work_group_id`|INTEGER NOT NULL|関連づけるWorkGroupの`id` (`work_group`で作成したWorkGroupのIDを指定する)|0 ~|
|`name`|TEXT NOT NULL|時刻表に表示するのに使用する文字列 (駅名の場合は4文字以下)|0 ~|
|`full_name`|TEXT|駅の完全な名前 (5文字以上の格納が可能)|1 ~|
|`location`|REAL NOT NULL|駅の位置 [m] (駅の並び順を決定するのに使用)|0 ~|
|`location_lon_deg`|REAL|駅の経度を度数法で表したもの [deg]|1 ~|
|`location_lat_deg`|REAL|駅の緯度を度数法で表したもの [deg]|1 ~|
|`on_station_detect_radius_m`|REAL|駅に在線しているかどうかを検出する円の半径 [m]|1 ~|
|`record_type`|INTEGER|駅の種類|1 ~|

なお、`record_type`にて使用する数字と意味の対照は以下の通りです。

|Value|Meaning|
|:---:|:---|
|0|通常の駅 (E電時刻表に表示させる)|
|1|通常の駅 (E電時刻表に表示させない)|
|2|情報を表示する行 (ほぼ全ての列車に表示させるもの)<br/>(例: 「交直切換」など)|
|3|情報を表示する行 (一部の列車に表示させるもの)<br/>(例: 工臨の現場情報など)|

## station_trackテーブル

https://github.com/TetsuOtter/TRViS/blob/eb35688f6f5544b1448f554b692f705f7a715141/TRViS.IO.Tests/Resources/CreateTables.sql#L70-L76

|Column|Type|Description|Version|
|---:|:---:|:---|:---:|
|`id`|INTEGER NOT NULL|Station_Trackテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|0 ~|
|`station_id`|INTEGER NOT NULL|関連づけるStationの`id` (`station`で作成したStationのIDを指定する)|0 ~|
|`name`|TEXT NOT NULL|「着線/発線」列に表示する名前 (全角2文字以下を推奨)<br/>(HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|

`name`には、例えば13番線であれば`１３`を指定します。

## timetable_rowテーブル

https://github.com/TetsuOtter/TRViS/blob/eb35688f6f5544b1448f554b692f705f7a715141/TRViS.IO.Tests/Resources/CreateTables.sql#L78-L111

|Column|Type|Description|Version|
|---:|:---:|:---|:---:|
|`id`|INTEGER NOT NULL|Timetable_Rowテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|0 ~|
|`train_id`|INTEGER NOT NULL|関連づけるTrainの`id` (`train`で作成したTrainのIDを指定する)|0 ~|
|`station_id`|INTEGER NOT NULL|関連づけるStationの`id` (`station`で作成したStationのIDを指定する)|0 ~|
|`drive_time_mm`|INTEGER|「運転時分」列の「分」部分に表示する数字 (NULLで非表示)|0 ~|
|`drive_time_ss`|INTEGER|「運転時分」列の「秒」部分に表示する数字 (NULLで非表示 0~9の場合は、`00`~`09`のように、ゼロ埋めして表示される)|0 ~|
|`is_operation_only_stop`|INTEGER|運転停車かどうか<br/>(`1`で「運転停車駅である」ということを表す。`0`または`NULL`で「運転停車駅ではない」ということを表す)|0 ~|
|`is_pass`|INTEGER|通過駅かどうか。通過駅の場合は着時刻と発時刻が赤文字で表示されます。<br/>(`1`で「通過駅である」ということを表す。`0`または`NULL`で「通過駅ではない」ということを表す)|0 ~|
|`has_bracket`|INTEGER|着時刻に`()`を表示するかどうか<br/>(`1`で「表示」、`0`または`NULL`で「非表示」)|0 ~|
|`is_last_stop`|INTEGER|終着駅かどうか (「発(通)」列に「=」を表示させるかどうか)<br/>(`1`で「表示」、`0`または`NULL`で「非表示」)|0 ~|
|`arrive_hh`|INTEGER|着時刻の「時」部分に表示する名前 (`NULL`で非表示)|0 ~|
|`arrive_mm`|INTEGER|着時刻の「分」部分に表示する名前 (`NULL`で非表示 但し、「時」が`NULL`でない場合は`00`が表示される)|0 ~|
|`arrive_ss`|INTEGER|着時刻の「秒」部分に表示する名前 (`NULL`で非表示)|0 ~|
|`arrive_str`|TEXT|「着」列に表示する文字列 (`NULL` または、着時刻が設定されている場合は表示されない。)<br/>`↓`を設定することで、通過を表す赤い矢印が表示される<br/>(HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`departure_hh`|INTEGER|発(通過)時刻の「時」部分に表示する名前 (`NULL`で非表示)|0 ~|
|`departure_mm`|INTEGER|発(通過)時刻の「分」部分に表示する名前 (`NULL`で非表示 但し、「時」が`NULL`でない場合は`00`が表示される)|0 ~|
|`departure_ss`|INTEGER|発(通過)時刻の「秒」部分に表示する名前 (`NULL`で非表示)|0 ~|
|`deaprture_str`|TEXT|「発(通)」列に表示する文字列 (`NULL` または、発(通過)時刻が設定されている場合は表示されない。)<br/>`↓`を設定することで、通過を表す赤い矢印が表示される<br/>(HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`station_track_id`|INTEGER|関連づけるStation_Trackの`id` (`station_track`で作成したStation_TrackのIDを指定する)<br/>`NULL`で非表示|0 ~|
|`run_in_limit`|INTEGER|「制限速度」列に進入制限として表示する速度。`NULL`で非表示|0 ~|
|`run_out_limit`|INTEGER|「制限速度」列に進出制限として表示する速度。`NULL`で非表示|0 ~|
|`remarks`|TEXT|「記事」列に表示する文字列 (HTMLを書くと、任意のスタイルで文字を表示できます)|0 ~|
|`marker_color_id`|INTEGER|デフォルトで表示させるマーカーの色ID|1 ~|
|`marker_text`|TEXT|デフォルトで表示させるマーカーのテキスト|1 ~|
|`work_type`|INTEGER|仕業の情報|1 ~|

なお、`record_type`にて使用する数字と意味の対照は以下の通りです。

|Value|Meaning|
|:---:|:---|
|0|なし|

(現在策定中)

## languageテーブル

https://github.com/TetsuOtter/TRViS/blob/eb35688f6f5544b1448f554b692f705f7a715141/TRViS.IO.Tests/Resources/CreateTables.sql#L113-L119

多言語対応用言語情報を記録するテーブル

現在準備中

## colorテーブル

https://github.com/TetsuOtter/TRViS/blob/eb35688f6f5544b1448f554b692f705f7a715141/TRViS.IO.Tests/Resources/CreateTables.sql#L161-L167

|Column|Type|Description|Version|
|---:|:---:|:---|:---:|
|`id`|INTEGER NOT NULL|Colorテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|1 ~|
|`name`|TEXT|色の名前|1 ~|
|`rgb`|INTEGER|色の情報 (`0xRRGGBB`)|1 ~|
