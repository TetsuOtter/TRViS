INSERT INTO "work_group"
(
  "id",
  "name",

  "db_version"
)
VALUES
(
  '1',
  'Group01',

  '1'
);

INSERT INTO "work"
(
  "id",
  "work_group_id",
  "name",
  "affect_date",

  "affix_content_type",
  "affix_content",
  "remarks",
  "has_e_train_timetable",
  "e_train_timetable_content_type",
  "e_train_timetable_content"
)
VALUES
(
  '1',
  '1',
  'Work01',
  '2022-09-15',

  NULL,
  NULL,
  'Work01 - Remarks',
  '1',
  NULL,
  NULL
),
(
  '2',
  '1',
  'Work02',
  '2022-09-15',

  NULL,
  NULL,
  'Work02 - Remarks',
  '2',
  NULL,
  NULL
),
(
  '3',
  '1',
  'Work03',
  '2022-09-15',

  NULL,
  NULL,
  'Work03 - Remarks',
  '3',
  NULL,
  NULL
);

INSERT INTO "station"
(
  "id",
  "work_group_id",
  "name",
  "location",

  "location_lon_deg",
  "location_lat_deg",
  "on_station_detect_radius_m",
  "full_name",
  "record_type"
)
VALUES
(
  '1',
  '1',
  'Station1',
  '1.0',

  NULL,
  NULL,
  NULL,
  NULL,
  NULL
),
(
  '2',
  '1',
  'Station2',
  '2.0',

  '135.5',
  '35.5',
  '200',
  'Station-2 Full Name',
  '1'
),
(
  '3',
  '1',
  'Station3',
  '3.0',

  NULL,
  NULL,
  NULL,
  NULL,
  NULL
),
(
  '4',
  '1',
  'Station4',
  '4.0',

  NULL,
  NULL,
  NULL,
  NULL,
  NULL
),
(
  '5',
  '1',
  'Station5',
  '5.0',

  NULL,
  NULL,
  NULL,
  NULL,
  NULL
);

INSERT INTO "station_track"
(
  "id",
  "station_id",
  "name"
)
VALUES
(
  '1',
  '1',
  '1-1'
),
(
  '2',
  '1',
  '1-2'
),
(
  '3',
  '2',
  '2-1'
),
(
  '4',
  '2',
  '2-2'
),
(
  '5',
  '3',
  '3-1'
);

INSERT INTO "train_data"
(
  "id",
  "work_id",
  "train_number",
  "max_speed",
  "speed_type",
  "ntc",
  "car_count",
  "destination",
  "begin_remarks",
  "after_remarks",
  "remarks",
  "before_departure",
  "train_info",
  "direction",

  "after_arrive",
  "before_departure_on_station_track_col",
  "after_arrive_on_station_track_col",
  "day_count",
  "is_ride_on_moving",
  "color_id"
)
VALUES
(
  '1',
  '1',
  'T9910X',
  '95',
  '高速特定',
  'E237系
1M',
  '1',
  '行き先',
  '〜試験用データ~',
  '〜試験用データ終わり~',
  '試験用データ',
  '発前点検300分',
  '試験用ダミーデータ',
  '1',

  '着後作業 10分',
  '点検',
  '作業',
  1,
  '0',
  NULL
);

INSERT INTO "timetable_row"
(
  "id",
  "train_id",
  "station_id",
  "drive_time_mm",
  "drive_time_ss",
  "is_operation_only_stop",
  "is_pass",
  "has_bracket",
  "is_last_stop",
  "arrive_hh",
  "arrive_mm",
  "arrive_ss",
  "departure_hh",
  "departure_mm",
  "departure_ss",
  "station_track_id",
  "run_in_limit",
  "run_out_limit",
  "remarks",
  "arrive_str",
  "departure_str",

  "marker_color_id",
  "marker_text",
  "work_type"
)
VALUES
(
  '1',
  '1',
  '1',
  '12',
  '34',
  '0',
  '0',
  '0',
  '0',
  NULL,
  NULL,
  NULL,
  '12',
  '34',
  '56',
  '1',
  NULL,
  NULL,
  'abc',
  NULL,
  NULL,

  NULL,
  '試験',
  NULL
),
(
  '2',
  '1',
  '2',
  '12',
  NULL,
  NULL,
  NULL,
  NULL,
  '1',
  NULL,
  NULL,
  NULL,
  NULL,
  NULL,
  NULL,
  NULL,
  NULL,
  NULL,
  NULL,
  '停車',
  NULL,

  NULL,
  NULL,
  '0'
);
