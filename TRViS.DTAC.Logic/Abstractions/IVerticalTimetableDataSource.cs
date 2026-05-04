namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Provides row layout data to <see cref="Presenter.VerticalTimetableViewPresenter"/>.
/// Raise <see cref="RowsChanged"/> whenever any of the layout properties change.
/// Raise <see cref="LocationMarkerStateChanged"/> / <see cref="LocationMarkerPositionChanged"/>
/// when the location marker changes.
/// </summary>
public interface IVerticalTimetableDataSource
{
	IReadOnlyList<bool> IsInfoRowList { get; }
	bool HasAfterRemarksText { get; }
	bool HasAfterArriveText { get; }
	bool HasNextTrainId { get; }

	event EventHandler? RowsChanged;
	event EventHandler<TimetableLocationState>? LocationMarkerStateChanged;
	event EventHandler<int>? LocationMarkerPositionChanged;
}
