using SQLite;

namespace TRViS.IO.Models.DB;

[Table("work_group")]
public record WorkGroup
{
	[PrimaryKey, AutoIncrement, Column("id"), NotNull, Unique]
	public int Id { get; set; }

	[Column("name"), NotNull]
	public string Name { get; set; } = "";
}

[Table("work")]
public record Work
{
	[PrimaryKey, AutoIncrement, Column("id"), NotNull, Unique]
	public int Id { get; set; }

	[Column("work_group_id"), NotNull]
	public int WorkGroupId { get; set; }

	[Column("name"), NotNull]
	public string Name { get; set; } = "";

	[Column("affect_date"), NotNull]
	public string AffectDate { get; set; } = "";
}

[Table("station")]
public record Station
{
	[PrimaryKey, AutoIncrement, Column("id"), NotNull, Unique]
	public int Id { get; set; }

	[Column("work_group_id"), NotNull]
	public int WorkGroupId { get; set; }

	[Column("name"), NotNull]
	public string Name { get; set; } = "";

	[Column("location"), NotNull]
	public double Location { get; set; }
}

[Table("station_track")]
public record StationTrack
{
	[PrimaryKey, AutoIncrement, Column("id"), NotNull, Unique]
	public int Id { get; set; }

	[Column("station_id"), NotNull]
	public int StationId { get; set; }

	[Column("name"), NotNull]
	public string Name { get; set; } = "";
}

[Table("train_data")]
public record TrainData
{
	[PrimaryKey, AutoIncrement, Column("id"), NotNull, Unique]
	public int Id { get; set; }

	[Column("work_id"), NotNull]
	public int WorkId { get; set; }

	[Column("train_number"), NotNull]
	public string TrainNumber { get; set; } = "";

	[Column("max_speed")]
	public string? MaxSpeed { get; set; }

	[Column("speed_type")]
	public string? SpeedType { get; set; }

	[Column("ntc")]
	public string? NominalTractiveCapacity { get; set; }

	[Column("car_count")]
	public int? CarCount { get; set; }

	[Column("destination")]
	public string? Destination { get; set; }

	[Column("begin_remarks")]
	public string? BeginRemarks { get; set; }

	[Column("after_remarks")]
	public string? AfterRemarks { get; set; }

	[Column("remarks")]
	public string? Remarks { get; set; }

	[Column("before_departure")]
	public string? BeforeDeparture { get; set; }

	[Column("train_info")]
	public string? TrainInfo { get; set; }

	[Column("direction"), NotNull]
	public int Direction { get; set; }
}

[Table("timetable_row")]
public record TimetableRowData
{
	[PrimaryKey, AutoIncrement, Column("id"), NotNull, Unique]
	public int Id { get; set; }

	[Column("train_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_timetablerow_1", Order = 1, Unique = true)]
	public int TrainId { get; set; }

	[Column("station_id"), NotNull]
	[Indexed(Name = "sqlite_autoindex_timetablerow_1", Order = 2, Unique = true)]
	public int StationId { get; set; }

	[Column("drive_time_mm")]
	public int? DriveTime_MM { get; set; }
	[Column("drive_time_ss")]
	public int? DriveTime_SS { get; set; }

	[Column("is_operation_only_stop")]
	public bool? IsOperationOnlyStop { get; set; }
	[Column("is_pass")]
	public bool? IsPass { get; set; }
	[Column("has_bracket")]
	public bool? HasBracket { get; set; }
	[Column("is_last_stop")]
	public bool? IsLastStop { get; set; }

	[Column("arrive_hh")]
	public int? Arrive_HH { get; set; }
	[Column("arrive_mm")]
	public int? Arrive_MM { get; set; }
	[Column("arrive_ss")]
	public int? Arrive_SS { get; set; }
	[Column("arrive_str")]
	public string? Arrive_Str { get; set; }

	[Column("departure_hh")]
	public int? Departure_HH { get; set; }
	[Column("departure_mm")]
	public int? Departure_MM { get; set; }
	[Column("departure_ss")]
	public int? Departure_SS { get; set; }
	[Column("departure_str")]
	public string? Departure_Str { get; set; }

	[Column("station_track_id")]
	public int? StationTrackId { get; set; }

	[Column("run_in_limit")]
	public int? RunInLimit { get; set; }
	[Column("run_out_limit")]
	public int? RunOutLimit { get; set; }

	[Column("remarks")]
	public string? Remarks { get; set; }
}