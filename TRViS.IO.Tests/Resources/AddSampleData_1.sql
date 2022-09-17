
INSERT INTO "work_group" ("id", "name") VALUES ('1', 'Group01');

INSERT INTO "work" ("id", "work_group_id", "name", "affect_date") VALUES ('1', '1', 'Work01', '2022-09-15');
INSERT INTO "work" ("id", "work_group_id", "name", "affect_date") VALUES ('2', '1', 'Work02', '2022-09-15');
INSERT INTO "work" ("id", "work_group_id", "name", "affect_date") VALUES ('3', '1', 'Work03', '2022-09-15');

INSERT INTO "station" ("id", "work_group_id", "name", "location") VALUES ('1', '1', 'Station1', '1.0');
INSERT INTO "station" ("id", "work_group_id", "name", "location") VALUES ('2', '1', 'Station2', '2.0');
INSERT INTO "station" ("id", "work_group_id", "name", "location") VALUES ('3', '1', 'Station3', '3.0');
INSERT INTO "station" ("id", "work_group_id", "name", "location") VALUES ('4', '1', 'Station4', '4.0');
INSERT INTO "station" ("id", "work_group_id", "name", "location") VALUES ('5', '1', 'Station5', '5.0');

INSERT INTO "station_track" ("id", "station_id", "name") VALUES ('1', '1', '1-1');
INSERT INTO "station_track" ("id", "station_id", "name") VALUES ('2', '1', '1-2');
INSERT INTO "station_track" ("id", "station_id", "name") VALUES ('3', '2', '2-1');
INSERT INTO "station_track" ("id", "station_id", "name") VALUES ('4', '2', '2-2');
INSERT INTO "station_track" ("id", "station_id", "name") VALUES ('5', '3', '3-1');

INSERT INTO "train_data" ("id", "work_id", "train_number", "max_speed", "speed_type", "ntc", "car_count", "destination", "begin_remarks", "after_remarks", "remarks", "before_departure", "train_info", "direction") VALUES ('1', '1', 'T9910X', '95', '高速特定', 'E237系
1M', '1', '行き先', '〜試験用データ~', '〜試験用データ終わり~', '試験用データ', '発前点検300分', '試験用ダミーデータ', '1');

INSERT INTO "timetable_row" ("id", "train_id", "station_id", "drivetime_mm", "drivetime_ss", "is_operation_only_stop", "is_pass", "has_bracket", "is_last_stop", "arrive_hh", "arrive_mm", "arrive_ss", "departure_hh", "departure_mm", "departure_ss", "station_trackid", "run_in_limit", "run_out_limit", "remarks", "arrive_str", "departure_str") VALUES ('1', '1', '1', '12', '34', '0', '0', '0', '0', NULL, NULL, NULL, '12', '34', '56', '1', NULL, NULL, 'abc', NULL, NULL);
INSERT INTO "timetable_row" ("id", "train_id", "station_id", "drivetime_mm", "drivetime_ss", "is_operation_only_stop", "is_pass", "has_bracket", "is_last_stop", "arrive_hh", "arrive_mm", "arrive_ss", "departure_hh", "departure_mm", "departure_ss", "station_trackid", "run_in_limit", "run_out_limit", "remarks", "arrive_str", "departure_str") VALUES ('2', '1', '2', '12', NULL, NULL, NULL, NULL, '1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '停車', NULL);
