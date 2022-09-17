CREATE TABLE "work_group" (
	"id"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	PRIMARY KEY("id" AUTOINCREMENT)
);

CREATE TABLE "work" (
	"id"	INTEGER NOT NULL,
	"work_group_id"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	"affect_date"	TEXT NOT NULL,
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
	FOREIGN KEY("work_id") REFERENCES "work"("id"),
	PRIMARY KEY("id" AUTOINCREMENT)
);

CREATE TABLE "station" (
	"id"	INTEGER NOT NULL,
	"work_group_id"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	"location"	REAL NOT NULL,
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

CREATE TABLE "timetablerow" (
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
	PRIMARY KEY("id" AUTOINCREMENT),
	UNIQUE("train_id", "station_id"),
	FOREIGN KEY("station_id") REFERENCES "station"("id"),
	FOREIGN KEY("station_track_id") REFERENCES "station_track"("id"),
	FOREIGN KEY("train_id") REFERENCES "train_data"("id")
);
