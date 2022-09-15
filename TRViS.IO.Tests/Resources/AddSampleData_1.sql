
INSERT INTO "workgroup" ("id", "name") VALUES ('1', 'Group01');

INSERT INTO "work" ("id", "workgroupid", "name", "affectdate") VALUES ('1', '1', 'Work01', '2022-09-15');
INSERT INTO "work" ("id", "workgroupid", "name", "affectdate") VALUES ('2', '1', 'Work02', '2022-09-15');
INSERT INTO "work" ("id", "workgroupid", "name", "affectdate") VALUES ('3', '1', 'Work03', '2022-09-15');

INSERT INTO "station" ("id", "workgroupid", "name", "location") VALUES ('1', '1', 'Station1', '1.0');
INSERT INTO "station" ("id", "workgroupid", "name", "location") VALUES ('2', '1', 'Station2', '2.0');
INSERT INTO "station" ("id", "workgroupid", "name", "location") VALUES ('3', '1', 'Station3', '3.0');
INSERT INTO "station" ("id", "workgroupid", "name", "location") VALUES ('4', '1', 'Station4', '4.0');
INSERT INTO "station" ("id", "workgroupid", "name", "location") VALUES ('5', '1', 'Station5', '5.0');

INSERT INTO "stationtrack" ("id", "stationid", "name") VALUES ('1', '1', '1-1');
INSERT INTO "stationtrack" ("id", "stationid", "name") VALUES ('2', '1', '1-2');
INSERT INTO "stationtrack" ("id", "stationid", "name") VALUES ('3', '2', '2-1');
INSERT INTO "stationtrack" ("id", "stationid", "name") VALUES ('4', '2', '2-2');
INSERT INTO "stationtrack" ("id", "stationid", "name") VALUES ('5', '3', '3-1');

INSERT INTO "traindata" ("id", "workid", "trainnumber", "maxspeed", "speedtype", "ntc", "carcount", "beginremarks", "remarks", "direction") VALUES ('1', '1', 'T9910X', '95', '高速特定', 'E237系
1M', '1', '〜試験用データ~', '試験用データ', '1');

INSERT INTO "timetablerow" ("id", "trainid", "stationid", "drivetime_mm", "drivetime_ss", "isoperationonlystop", "ispass", "hasbracket", "islaststop", "arrive_hh", "arrive_mm", "arrive_ss", "departure_hh", "departure_mm", "departure_ss", "stationtrackid", "runinlimit", "runoutlimit", "remarks", "arrive_str", "departure_str") VALUES ('1', '1', '1', '12', '34', '0', '0', '0', '0', NULL, NULL, NULL, '12', '34', '56', '1', NULL, NULL, 'abc', NULL, NULL);
INSERT INTO "timetablerow" ("id", "trainid", "stationid", "drivetime_mm", "drivetime_ss", "isoperationonlystop", "ispass", "hasbracket", "islaststop", "arrive_hh", "arrive_mm", "arrive_ss", "departure_hh", "departure_mm", "departure_ss", "stationtrackid", "runinlimit", "runoutlimit", "remarks", "arrive_str", "departure_str") VALUES ('2', '1', '2', '12', NULL, NULL, NULL, NULL, '1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '停車', NULL);
