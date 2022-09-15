CREATE TABLE "workgroup" (
	"id"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	PRIMARY KEY("id" AUTOINCREMENT)
);

CREATE TABLE "work" (
	"id"	INTEGER NOT NULL,
	"workgroupid"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	"affectdate"	TEXT NOT NULL,
	FOREIGN KEY("workgroupid") REFERENCES "workgroup"("id"),
	PRIMARY KEY("id" AUTOINCREMENT)
);

CREATE TABLE "traindata" (
	"id"	INTEGER NOT NULL,
	"workid"	INTEGER NOT NULL,
	"trainnumber"	TEXT NOT NULL,
	"maxspeed"	TEXT,
	"speedtype"	TEXT,
	"ntc"	TEXT,
	"carcount"	INTEGER,
	"beginremarks"	TEXT,
	"remarks"	TEXT, direction INTEGER NOT NULL,
	FOREIGN KEY("workid") REFERENCES "work"("id"),
	PRIMARY KEY("id" AUTOINCREMENT)
);

CREATE TABLE "station" (
	"id"	INTEGER NOT NULL,
	"workgroupid"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	"location"	REAL NOT NULL,
	PRIMARY KEY("id" AUTOINCREMENT),
	FOREIGN KEY("workgroupid") REFERENCES "workgroup"("id")
);

CREATE TABLE "stationtrack" (
	"id"	INTEGER NOT NULL,
	"stationid"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	PRIMARY KEY("id" AUTOINCREMENT),
	FOREIGN KEY("stationid") REFERENCES "station"("id")
);

CREATE TABLE "timetablerow" (
	"id"	INTEGER NOT NULL,
	"trainid"	INTEGER NOT NULL,
	"stationid"	INTEGER NOT NULL,
	"drivetime_mm"	INTEGER,
	"drivetime_ss"	INTEGER,
	"isoperationonlystop"	INTEGER,
	"ispass"	INTEGER,
	"hasbracket"	INTEGER,
	"islaststop"	INTEGER,
	"arrive_hh"	INTEGER,
	"arrive_mm"	INTEGER,
	"arrive_ss"	INTEGER,
	"departure_hh"	INTEGER,
	"departure_mm"	INTEGER,
	"departure_ss"	INTEGER,
	"stationtrackid"	INTEGER,
	"runinlimit"	INTEGER,
	"runoutlimit"	INTEGER,
	"remarks"	TEXT, arrive_str TEXT, departure_str TEXT,
	PRIMARY KEY("id" AUTOINCREMENT),
	UNIQUE("trainid", "stationid"),
	FOREIGN KEY("stationid") REFERENCES "station"("id"),
	FOREIGN KEY("stationtrackid") REFERENCES "stationtrack"("id"),
	FOREIGN KEY("trainid") REFERENCES "traindata"("id")
);
