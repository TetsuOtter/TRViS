namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Provides row layout data to <see cref="Presenter.VerticalTimetableViewPresenter"/>.
/// Raise <see cref="RowsChanged"/> whenever any of the properties change.
/// </summary>
public interface IVerticalTimetableDataSource
{
	IReadOnlyList<bool> IsInfoRowList { get; }
	bool HasAfterRemarksText { get; }
	bool HasAfterArriveText { get; }
	bool HasNextTrainId { get; }

	event EventHandler? RowsChanged;
}
