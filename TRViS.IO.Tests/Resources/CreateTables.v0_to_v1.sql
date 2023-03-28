-- region v1 table (color)

CREATE TABLE "color" (
	"id" INTEGER NOT NULL,
	"name" TEXT NOT NULL,
	"rgb" INTEGER NOT NULL,

	PRIMARY KEY("id" AUTOINCREMENT)
);

-- endregion

-- region v1 columns

ALTER TABLE "work_group" ADD COLUMN "db_version" INTEGER;

ALTER TABLE "work" ADD COLUMN "affix_content_type" INTEGER;
ALTER TABLE "work" ADD COLUMN "affix_content" BLOB;
ALTER TABLE "work" ADD COLUMN "remarks" TEXT;
ALTER TABLE "work" ADD COLUMN "has_e_train_timetable" INTEGER;
ALTER TABLE "work" ADD COLUMN "e_train_timetable_content_type" INTEGER;
ALTER TABLE "work" ADD COLUMN "e_train_timetable_content" BLOB;

ALTER TABLE "train_data" ADD COLUMN "after_arrive" TEXT;
ALTER TABLE "train_data" ADD COLUMN "before_departure_on_station_track_col" TEXT;
ALTER TABLE "train_data" ADD COLUMN "after_arrive_on_station_track_col" TEXT;
ALTER TABLE "train_data" ADD COLUMN "day_count" INTEGER;
ALTER TABLE "train_data" ADD COLUMN "is_ride_on_moving" INTEGER;
ALTER TABLE "train_data" ADD COLUMN "color_id" INTEGER REFERENCES "color"("id");

ALTER TABLE "station" ADD COLUMN "location_lon_deg" REAL;
ALTER TABLE "station" ADD COLUMN "location_lat_deg" REAL;
ALTER TABLE "station" ADD COLUMN "on_station_detect_radius_m" REAL;
ALTER TABLE "station" ADD COLUMN "full_name" TEXT;
ALTER TABLE "station" ADD COLUMN "record_type" INTEGER;

ALTER TABLE "timetable_row" ADD COLUMN "marker_color_id" INTEGER REFERENCES "color"("id");
ALTER TABLE "timetable_row" ADD COLUMN "marker_text" TEXT;
ALTER TABLE "timetable_row" ADD COLUMN "work_type" INTEGER;

-- endregion

-- region v1 tables

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

-- endregion
