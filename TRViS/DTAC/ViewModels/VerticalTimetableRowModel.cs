using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO.Models;

namespace TRViS.DTAC.ViewModels;

public partial class VerticalTimetableRowModel : ObservableObject
{
	public enum LocationStates
	{
		Undefined,
		AroundThisStation,
		RunningToNextStation
	}

	[ObservableProperty]
	public partial int RowIndex { get; set; } = -1;

	[ObservableProperty]
	public partial bool IsInfoRow { get; set; } = false;
	[ObservableProperty]
	public partial string? InfoText { get; set; } = null;

	[ObservableProperty]
	public partial bool IsMarkingMode { get; set; } = false;

	[ObservableProperty]
	public partial string? DriveTimeMM { get; set; } = null;
	[ObservableProperty]
	public partial string? DriveTimeSS { get; set; } = null;
	[ObservableProperty]
	public partial string StationName { get; set; } = string.Empty;
	[ObservableProperty]
	public partial bool IsPass { get; set; } = false;
	[ObservableProperty]
	public partial TimeData? ArrivalTime { get; set; } = null;
	[ObservableProperty]
	public partial bool HasBracket { get; set; } = false;
	[ObservableProperty]
	public partial TimeData? DepartureTime { get; set; } = null;
	[ObservableProperty]
	public partial bool IsLastStop { get; set; } = false;
	[ObservableProperty]
	public partial bool IsOperationOnlyStop { get; set; } = false;
	[ObservableProperty]
	public partial string? TrackName { get; set; } = null;
	[ObservableProperty]
	public partial string? RunInLimit { get; set; } = null;
	[ObservableProperty]
	public partial string? RunOutLimit { get; set; } = null;
	[ObservableProperty]
	public partial string? Remarks { get; set; } = null;
	[ObservableProperty]
	public partial Color? MarkerColor { get; set; } = null;
	[ObservableProperty]
	public partial string? MarkerText { get; set; } = null;

	[ObservableProperty]
	public partial bool IsLocationMarkerOnThisRow { get; set; } = false;

	public void MarkerBoxTapped(Color? selectedColor, string? selectedText)
	{
		if (MarkerColor is null)
		{
			MarkerColor = selectedColor;
			MarkerText = selectedText ?? string.Empty;
		}
		else
		{
			MarkerColor = null;
			MarkerText = null;
		}
	}
}
