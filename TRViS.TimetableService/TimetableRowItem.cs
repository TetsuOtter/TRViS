namespace TRViS.TimetableService;

/// <summary>
/// Represents a timetable row item with a unique ID for tracking insertions and deletions
/// </summary>
public class TimetableRowItem
{
	public string Id { get; set; } = string.Empty;
	public LocationInfoItem? Location { get; set; }
	public int? DriveTimeMM { get; set; }
	public int? DriveTimeSS { get; set; }
	public string StationName { get; set; } = string.Empty;
	public bool IsOperationOnlyStop { get; set; }
	public bool IsPass { get; set; }
	public bool HasBracket { get; set; }
	public bool IsLastStop { get; set; }
	public TimeDataItem? ArriveTime { get; set; }
	public TimeDataItem? DepartureTime { get; set; }
	public string? TrackName { get; set; }
	public int? RunInLimit { get; set; }
	public int? RunOutLimit { get; set; }
	public string? Remarks { get; set; }
	public bool IsInfoRow { get; set; }
	public int? DefaultMarkerColor_RGB { get; set; }
	public string? DefaultMarkerText { get; set; }
}

/// <summary>
/// Location information
/// </summary>
public class LocationInfoItem
{
	public double Lat { get; set; }
	public double Lon { get; set; }
	public int? Radius { get; set; }
}

/// <summary>
/// Time data
/// </summary>
public class TimeDataItem
{
	public int? Hour { get; set; }
	public int? Minutes { get; set; }
	public int? Seconds { get; set; }
}
