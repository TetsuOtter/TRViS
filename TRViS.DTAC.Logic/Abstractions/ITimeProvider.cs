namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Provides time-change notifications (elapsed seconds from timetable reference).
/// </summary>
public interface ITimeProvider
{
	/// <summary>
	/// Fired when the timetable clock time changes.
	/// The event argument is total seconds (may be negative before departure).
	/// </summary>
	event EventHandler<int>? TimeChanged;

	/// <summary>
	/// Returns the current timetable clock time in total seconds.
	/// Used to prime the initial display without waiting for the first TimeChanged event.
	/// </summary>
	int GetCurrentTimeSeconds();
}
