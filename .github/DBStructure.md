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

### 注意事項

HTMLで表示内容を書くことができるColumnがありますが、HTMLを利用する場合は**何らかのタグで表示内容全体を囲んで**記録してください。
実装の都合上、文字列の先頭に`<`、加えて末尾に`>`が存在する場合にHTMLで描画するようにしています。

例えば、一部を太文字にしたい場合は、`ABC<b>D</b>EF`ではなく、`<span>ABC<b>D</b>EF</span>`のようにします。

### work_groupテーブル

https://github.com/TetsuOtter/TRViS/blob/97de680106a7219d22753f4174b6167342e1f700/TRViS.IO.Tests/Resources/CreateTables.sql#L1-L5

|Column|Type|Description|
|---:|:---:|:---|
|`id`|INTEGER NOT NULL|WorkGroupテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|
|`name`|TEXT NOT NULL|WorkGroupの名前|

### workテーブル

https://github.com/TetsuOtter/TRViS/blob/97de680106a7219d22753f4174b6167342e1f700/TRViS.IO.Tests/Resources/CreateTables.sql#L7-L14

|Column|Type|Description|
|---:|:---:|:---|
|`id`|INTEGER NOT NULL|Workテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|
|`work_group_id`|INTEGER NOT NULL|関連づけるWorkGroupの`id` (`work_group`で作成したWorkGroupのIDを指定する)|
|`name`|TEXT NOT NULL|WorkGroupの名前|

### train_dataテーブル

列車ごとの情報を記録するテーブルです。各駅の停車時刻等は`timetable_row`テーブルに記録します。

https://github.com/TetsuOtter/TRViS/blob/a02427bd35a93f4f64046685b68533f858809d7b/TRViS.IO.Tests/Resources/CreateTables.sql#L16-L33

|Column|Type|Description|
|---:|:---:|:---|
|`id`|INTEGER NOT NULL|TrainDataテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|
|`work_id`|INTEGER NOT NULL|関連づけるWorkの`id` (`work_group`で作成したWorkGroupのIDを指定する)|
|`train_number`|TEXT NOT NULL|列車番号 (「列番」として表示される)|
|`max_speed`|TEXT|「最高速度」として表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|
|`speed_type`|TEXT|「速度種別」として表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|
|`ntc`|TEXT|「けん引定数」として表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|
|`car_count`|INTEGER|編成両数。`NULL`にしても、両数表示は消えません。|
|`destination`|TEXT|「`(XXX 行)`」の`XXX`部分に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)<br/>NULLにすると`(XXX 行)`部分が丸ごと表示されなくなります。|
|`begin_remarks`|TEXT|「着」の直上あたりに表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|
|`after_remarks`|TEXT|時刻表部分の一番下に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|
|`remarks`|TEXT|「注意事項」に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|
|`before_departure`|TEXT|「発前」に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|
|`train_info`|TEXT|「発前」の上部分に表示される内容 (HTMLを書くと、任意のスタイルで文字を表示できます)|
|`direction`|INTEGER NOT NULL|列車の進行方向を表す値<br/>0以上の値を設定することで、駅位置について昇順(`0 to 9`の順)で表示されます。逆に、0未満で降順(`9 to 0`)です。|

なお、`v0.0.1-3`の時点では、`destination`、`after_remarks`、`remarks`、`before_departure`、`train_info`が未実装です。
データをDBに含めることはできますが、現時点で、アプリ内で表示させることはできません。

### stationテーブル

各駅の情報を記録するテーブルです。駅の情報はWorkGroupごとに分けることができます。

https://github.com/TetsuOtter/TRViS/blob/a02427bd35a93f4f64046685b68533f858809d7b/TRViS.IO.Tests/Resources/CreateTables.sql#L35-L42

|Column|Type|Description|
|---:|:---:|:---|
|`id`|INTEGER NOT NULL|Stationテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|
|`work_group_id`|INTEGER NOT NULL|関連づけるWorkGroupの`id` (`work_group`で作成したWorkGroupのIDを指定する)|
|`name`|TEXT NOT NULL|駅の名前 (4文字以下で指定)|
|`full_name`|TEXT|駅の完全な名前 (5文字以上の格納が可能)|
|`location`|REAL NOT NULL|駅の位置 [m] (駅の並び順を決定するのに使用)|

駅名の全体を表す`full_name`は、現状アプリ内で実装されていません。
上で示したSQLにも入れ忘れているので、このカラムの追加は任意です。

### station_trackテーブル

https://github.com/TetsuOtter/TRViS/blob/a02427bd35a93f4f64046685b68533f858809d7b/TRViS.IO.Tests/Resources/CreateTables.sql#L44-L50

|Column|Type|Description|
|---:|:---:|:---|
|`id`|INTEGER NOT NULL|Station_Trackテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|
|`station_id`|INTEGER NOT NULL|関連づけるStationの`id` (`station`で作成したStationのIDを指定する)|
|`name`|TEXT NOT NULL|「着線/発線」列に表示する名前 (全角2文字以下を推奨)<br/>(HTMLを書くと、任意のスタイルで文字を表示できます)|

`name`には、例えば13番線であれば`１３`を指定します。

### timetable_rowテーブル

https://github.com/TetsuOtter/TRViS/blob/a02427bd35a93f4f64046685b68533f858809d7b/TRViS.IO.Tests/Resources/CreateTables.sql#L52-L79

|Column|Type|Description|
|---:|:---:|:---|
|`id`|INTEGER NOT NULL|Timetable_Rowテーブル内での一意の番号 (`AUTOINCREMENT`なため、自動で付与される)|
|`train_id`|INTEGER NOT NULL|関連づけるTrainの`id` (`train`で作成したTrainのIDを指定する)|
|`station_id`|INTEGER NOT NULL|関連づけるStationの`id` (`station`で作成したStationのIDを指定する)|
|`drive_time_mm`|INTEGER|「運転時分」列の「分」部分に表示する数字 (NULLで非表示)|
|`drive_time_ss`|INTEGER|「運転時分」列の「秒」部分に表示する数字 (NULLで非表示 0~9の場合は、`00`~`09`のように、ゼロ埋めして表示される)|
|`is_operation_only_stop`|INTEGER|運転停車かどうか<br/>(`1`で「運転停車駅である」ということを表す。`0`または`NULL`で「運転停車駅ではない」ということを表す)|
|`is_pass`|INTEGER|通過駅かどうか。通過駅の場合は着時刻と発時刻が赤文字で表示されます。<br/>(`1`で「通過駅である」ということを表す。`0`または`NULL`で「通過駅ではない」ということを表す)|
|`has_bracket`|INTEGER|着時刻に`()`を表示するかどうか<br/>(`1`で「表示」、`0`または`NULL`で「非表示」)|
|`is_last_stop`|INTEGER|終着駅かどうか (「発(通)」列に「=」を表示させるかどうか)<br/>(`1`で「表示」、`0`または`NULL`で「非表示」)|
|`arrive_hh`|INTEGER|着時刻の「時」部分に表示する名前 (`NULL`で非表示)|
|`arrive_mm`|INTEGER|着時刻の「分」部分に表示する名前 (`NULL`で非表示 但し、「時」が`NULL`でない場合は`00`が表示される)|
|`arrive_ss`|INTEGER|着時刻の「秒」部分に表示する名前 (`NULL`で非表示)|
|`arrive_str`|TEXT|「着」列に表示する文字列 (`NULL` または、着時刻が設定されている場合は表示されない。)<br/>`↓`を設定することで、通過を表す赤い矢印が表示される<br/>(HTMLを書くと、任意のスタイルで文字を表示できます)|
|`departure_hh`|INTEGER|発(通過)時刻の「時」部分に表示する名前 (`NULL`で非表示)|
|`departure_mm`|INTEGER|発(通過)時刻の「分」部分に表示する名前 (`NULL`で非表示 但し、「時」が`NULL`でない場合は`00`が表示される)|
|`departure_ss`|INTEGER|発(通過)時刻の「秒」部分に表示する名前 (`NULL`で非表示)|
|`deaprture_str`|TEXT|「発(通)」列に表示する文字列 (`NULL` または、発(通過)時刻が設定されている場合は表示されない。)<br/>`↓`を設定することで、通過を表す赤い矢印が表示される<br/>(HTMLを書くと、任意のスタイルで文字を表示できます)|
|`station_track_id`|INTEGER|関連づけるStation_Trackの`id` (`station_track`で作成したStation_TrackのIDを指定する)<br/>`NULL`で非表示|
|`run_in_limit`|INTEGER|「制限速度」列に進入制限として表示する速度。`NULL`で非表示|
|`run_out_limit`|INTEGER|「制限速度」列に進出制限として表示する速度。`NULL`で非表示|
|`remarks`|TEXT|「記事」列に表示する文字列 (HTMLを書くと、任意のスタイルで文字を表示できます)|
