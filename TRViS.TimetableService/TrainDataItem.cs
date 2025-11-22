namespace TRViS.TimetableService;

/// <summary>
/// Represents a train data item managed by the timetable service.
/// Maintains an ordered collection of timetable rows with unique IDs.
/// </summary>
public class TrainDataItem
{
	public string Id { get; set; } = string.Empty;
	public string? WorkName { get; set; }
	public DateOnly? AffectDate { get; set; }
	public string? TrainNumber { get; set; }
	public string? MaxSpeed { get; set; }
	public string? SpeedType { get; set; }
	public string? NominalTractiveCapacity { get; set; }
	public int? CarCount { get; set; }
	public string? Destination { get; set; }
	public string? BeginRemarks { get; set; }
	public string? AfterRemarks { get; set; }
	public string? Remarks { get; set; }
	public string? BeforeDeparture { get; set; }
	public string? TrainInfo { get; set; }
	public List<TimetableRowItem> Rows { get; set; } = new();
	public int Direction { get; set; }
	public string? AfterArrive { get; set; }
	public string? BeforeDepartureOnStationTrackCol { get; set; }
	public string? AfterArriveOnStationTrackCol { get; set; }
	public int DayCount { get; set; }
	public bool? IsRideOnMoving { get; set; }
	public int? LineColor_RGB { get; set; }
	public string? NextTrainId { get; set; }
}
