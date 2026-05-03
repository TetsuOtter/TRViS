namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Represents the current location state of the timetable marker.
/// Mirrors <c>VerticalTimetableRowModel.LocationStates</c> in the View layer.
/// </summary>
public enum TimetableLocationState
{
	/// <summary>No location is being tracked.</summary>
	Undefined,

	/// <summary>The train is around / stopped at this station.</summary>
	AroundThisStation,

	/// <summary>The train is running toward the next station.</summary>
	RunningToNextStation,
}
