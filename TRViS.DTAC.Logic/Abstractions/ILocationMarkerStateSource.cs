namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Provides the per-row location states that drive the timetable location marker.
/// Implemented by <c>VerticalStylePagePresenter</c> so the timetable view presenter
/// can subscribe directly without going through the View layer.
/// </summary>
public interface ILocationMarkerStateSource
{
	IReadOnlyDictionary<int, VerticalTimetableRowState> RowStates { get; }

	event EventHandler<VerticalPageStateChangedEventArgs>? StateChanged;
}
