CREATE TABLE "work_group" (
	"id"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,

	"db_version"	INTEGER,
	PRIMARY KEY("id" AUTOINCREMENT)
);

CREATE TABLE "work" (
	"id"	INTEGER NOT NULL,
	"work_group_id"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	"affect_date"	TEXT NOT NULL,

	"affix_content_type"	INTEGER,
	"affix_content"	BLOB,
	"remarks" TEXT,
	"has_e_train_timetable" INTEGER,
	"e_train_timetable_content_type" INTEGER,
	"e_train_timetable_content" BLOB,

	FOREIGN KEY("work_group_id") REFERENCES "work_group"("id"),
	PRIMARY KEY("id" AUTOINCREMENT)
);

CREATE TABLE "train_data" (
	"id"	INTEGER NOT NULL,
	"work_id"	INTEGER NOT NULL,
	"train_number"	TEXT NOT NULL,
	"max_speed"	TEXT,
	"speed_type"	TEXT,
	"ntc"	TEXT,
	"car_count"	INTEGER,
	"destination"	TEXT,
	"begin_remarks"	TEXT,
	"after_remarks"	TEXT,
	"remarks"	TEXT,
	"before_departure"	TEXT,
	"train_info"	TEXT,
	"direction" INTEGER NOT NULL,

	"after_arrive" TEXT,
	"before_departure_on__station_track_col" TEXT,
	"after_arrive_on__station_track_col" TEXT,
	"day_count" INTEGER,
	"is_ride_on_moving" INTEGER,
	"color_id" INTEGER,

	FOREIGN KEY("work_id") REFERENCES "work"("id"),
	FOREIGN KEY("color_id") REFERENCES "color"("id"),
	PRIMARY KEY("id" AUTOINCREMENT)
);

CREATE TABLE "station" (
	"id"	INTEGER NOT NULL,
	"work_group_id"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	"location"	REAL NOT NULL,

	"location_lon_deg"	REAL,
	"location_lat_deg"	REAL,
	"on_station_detect_radius_m"	REAL,
	"full_name"	TEXT,
	"record_type"	INTEGER,

	PRIMARY KEY("id" AUTOINCREMENT),
	FOREIGN KEY("work_group_id") REFERENCES "work_group"("id")
);

CREATE TABLE "station_track" (
	"id"	INTEGER NOT NULL,
	"station_id"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	PRIMARY KEY("id" AUTOINCREMENT),
	FOREIGN KEY("station_id") REFERENCES "station"("id")
);

CREATE TABLE "timetable_row" (
	"id"	INTEGER NOT NULL,
	"train_id"	INTEGER NOT NULL,
	"station_id"	INTEGER NOT NULL,
	"drive_time_mm"	INTEGER,
	"drive_time_ss"	INTEGER,
	"is_operation_only_stop"	INTEGER,
	"is_pass"	INTEGER,
	"has_bracket"	INTEGER,
	"is_last_stop"	INTEGER,
	"arrive_hh"	INTEGER,
	"arrive_mm"	INTEGER,
	"arrive_ss"	INTEGER,
	"departure_hh"	INTEGER,
	"departure_mm"	INTEGER,
	"departure_ss"	INTEGER,
	"station_track_id"	INTEGER,
	"run_in_limit"	INTEGER,
	"run_out_limit"	INTEGER,
	"remarks"	TEXT,
	"arrive_str" TEXT,
	"departure_str" TEXT,

	"marker_color_id" INTEGER,
	"marker_text" TEXT,
	"work_type" INTEGER,

	PRIMARY KEY("id" AUTOINCREMENT),
	UNIQUE("train_id", "station_id"),
	FOREIGN KEY("station_id") REFERENCES "station"("id"),
	FOREIGN KEY("station_track_id") REFERENCES "station_track"("id"),
	FOREIGN KEY("train_id") REFERENCES "train_data"("id"),
	FOREIGN KEY("marker_color_id") REFERENCES "color"("id")
);

CREATE TABLE "language" (
	"id" INTEGER NOT NULL,
	"language_code" TEXT NOT NULL,

	PRIMARY KEY("id" AUTOINCREMENT),
	UNIQUE("language_code")
);

CREATE TABLE "work_group_name_other_lang" (
	"work_group_id" INTEGER NOT NULL,
	"language_id" INTEGER NOT NULL,
	"name" TEXT NOT NULL,

	PRIMARY KEY("work_group_id", "language_id"),
	FOREIGN KEY("work_group_id") REFERENCES "work_group"("id"),
	FOREIGN KEY("language_id") REFERENCES "language"("id")
);

CREATE TABLE "work_name_other_lang" (
	"work_id" INTEGER NOT NULL,
	"language_id" INTEGER NOT NULL,
	"name" TEXT NOT NULL,

	PRIMARY KEY("work_id", "language_id"),
	FOREIGN KEY("work_id") REFERENCES "work"("id"),
	FOREIGN KEY("language_id") REFERENCES "language"("id")
);

CREATE TABLE "station_name_other_lang" (
	"station_id" INTEGER NOT NULL,
	"language_id" INTEGER NOT NULL,
	"name" TEXT NOT NULL,

	PRIMARY KEY("station_id", "language_id"),
	FOREIGN KEY("station_id") REFERENCES "station"("id"),
	FOREIGN KEY("language_id") REFERENCES "language"("id")
);

CREATE TABLE "station_track_name_other_lang" (
	"station_track_id" INTEGER NOT NULL,
	"language_id" INTEGER NOT NULL,
	"name" TEXT NOT NULL,

	PRIMARY KEY("station_track_id", "language_id"),
	FOREIGN KEY("station_track_id") REFERENCES "station_track"("id"),
	FOREIGN KEY("language_id") REFERENCES "language"("id")
);

CREATE TABLE "color" (
	"id" INTEGER NOT NULL,
	"name" TEXT NOT NULL,
	"rgb" INTEGER NOT NULL,

	PRIMARY KEY("id" AUTOINCREMENT)
);
