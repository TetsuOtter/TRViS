using SQLite;

namespace TRViS.IO.Models.DB;

[Table("workgroup")]
public class WorkGroup
{
	[PrimaryKey, AutoIncrement, Column("id")]
	public int Id { get; set; }

	[Column("name"), NotNull]
	public string Name { get; set; } = "";
}

[Table("work")]
public class Work
{
	[PrimaryKey, AutoIncrement, Column("id")]
	public int Id { get; set; }

	[Column("workgroupid"), NotNull]
	public int WorkGroupId { get; set; }

	[Column("name"), NotNull]
	public string Name { get; set; } = "";

	[Column("affectdate"), NotNull]
	public string AffectDate { get; set; } = "";
}

[Table("station")]
public class Station
{
	[PrimaryKey, AutoIncrement, Column("id")]
	public int Id { get; set; }

	[Column("workgroupid"), NotNull]
	public int WorkGroupId { get; set; }

	[Column("name"), NotNull]
	public string Name { get; set; } = "";

	[Column("location"), NotNull]
	public double Location { get; set; }
}

[Table("stationtrack")]
public class StationTrack
{
	[PrimaryKey, AutoIncrement, Column("id")]
	public int Id { get; set; }

	[Column("stationid"), NotNull]
	public int StationId { get; set; }

	[Column("name"), NotNull]
	public string Name { get; set; } = "";
}

[Table("traindata")]
public class TrainData
{
	[PrimaryKey, AutoIncrement, Column("id")]
	public int Id { get; set; }

	[Column("workid"), NotNull]
	public int WorkId { get; set; }

	[Column("trainnumber"), NotNull]
	public string TrainNumber { get; set; } = "";

	[Column("maxspeed")]
	public string? MaxSpeed { get; set; } = "";

	[Column("speedtype")]
	public string? SpeedType { get; set; } = "";

	[Column("ntc")]
	public string? NominalTractiveCapacity { get; set; } = "";

	[Column("carcount")]
	public int? CarCount { get; set; }

	[Column("beginremarks")]
	public string? BeginRemarks { get; set; } = "";

	[Column("remarks")]
	public string? Remarks { get; set; } = "";

	[Column("direction"), NotNull]
	public int Direction { get; set; }
}

[Table("timetablerow")]
public class TimetableRowData
{
	[PrimaryKey, AutoIncrement, Column("id")]
	public int Id { get; set; }

	[Column("trainid"), NotNull]
	public int TrainId { get; set; }

	[Column("stationid"), NotNull]
	public int StationId { get; set; }

	[Column("drivetime_mm")]
	public int? DriveTime_MM { get; set; }
	[Column("drivetime_ss")]
	public int? DriveTime_SS { get; set; }

	[Column("isoperationonlystop")]
	public bool? IsOperationOnlyStop { get; set; }
	[Column("ispass")]
	public bool? IsPass { get; set; }
	[Column("hasbracket")]
	public bool? HasBracket { get; set; }
	[Column("islaststop")]
	public bool? IsLastStop { get; set; }

	[Column("arrive_hh")]
	public int? Arrive_HH { get; set; }
	[Column("arrive_mm")]
	public int? Arrive_MM { get; set; }
	[Column("arrive_ss")]
	public int? Arrive_SS { get; set; }

	[Column("departure_hh")]
	public int? Departure_HH { get; set; }
	[Column("departure_mm")]
	public int? Departure_MM { get; set; }
	[Column("departure_ss")]
	public int? Departure_SS { get; set; }

	[Column("stationtrackid")]
	public int? StationTrackId { get; set; }

	[Column("runinlimit")]
	public int? RunInLimit { get; set; }
	[Column("runoutlimit")]
	public int? RunOutLimit { get; set; }

	[Column("remarks")]
	public string? Remarks { get; set; }
}