using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Layout;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Lightweight presenter for <c>VerticalTimetableView</c>.
/// Handles layout calculations and marker state aggregation.
/// Row interaction (run/location service state machine) is handled by
/// <see cref="VerticalStylePagePresenter"/>; this presenter focuses only on
/// what is exclusive to the timetable grid widget.
/// </summary>
public sealed class VerticalTimetableViewPresenter : IDisposable
{
	private readonly IMarkerToggleController _markerToggle;
	private readonly IDtacCrashLogger _crashLogger;
	private readonly IVerticalTimetableDataSource _dataSource;
	private readonly ILocationMarkerStateSource _locationMarkerSource;

	// Persisted layout inputs so we can re-compute on partial changes
	private int _rowCount = 0;
	private bool _hasAfterRemarks = false;
	private bool _hasAfterArrive = false;
	private bool _hasNextTrainButton = false;

	private VerticalTimetableViewPageState _currentState = new();
	private bool _disposed = false;

	/// <summary>Current aggregate state (never null).</summary>
	public VerticalTimetableViewPageState CurrentState => _currentState;

	/// <summary>Raised whenever any part of <see cref="CurrentState"/> changes.</summary>
	public event EventHandler<VerticalTimetableViewStateChangedEventArgs>? StateChanged;

	/// <summary>
	/// Raised when the view should scroll to the given row index.
	/// </summary>
	public event EventHandler<int>? ScrollRequested;

	public VerticalTimetableViewPresenter(
		IMarkerToggleController markerToggle,
		IDtacCrashLogger crashLogger,
		IVerticalTimetableDataSource dataSource,
		ILocationMarkerStateSource locationMarkerSource)
	{
		_markerToggle = markerToggle ?? throw new ArgumentNullException(nameof(markerToggle));
		_crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));
		_dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
		_locationMarkerSource = locationMarkerSource ?? throw new ArgumentNullException(nameof(locationMarkerSource));

		_markerToggle.PropertyChanged += OnMarkerTogglePropertyChanged;
		_dataSource.RowsChanged += OnDataSourceRowsChanged;
		_locationMarkerSource.StateChanged += OnLocationMarkerSourceStateChanged;

		// Sync initial state
		_currentState.IsMarkingMode = _markerToggle.IsToggled;
	}

	// ---------- Intents from View ----------

	/// <summary>
	/// Called when the user activates the marker toggle.
	/// Delegates to <see cref="IMarkerToggleController.Toggle"/>; the resulting
	/// <see cref="IMarkerToggleController.IsToggled"/> change is observed internally.
	/// </summary>
	public void OnMarkerToggled()
	{
		_markerToggle.Toggle();
	}

	// ---------- Private helpers ----------

	private void OnDataSourceRowsChanged(object? sender, EventArgs e)
	{
		_rowCount = _dataSource.IsInfoRowList.Count;
		_hasAfterRemarks = _dataSource.HasAfterRemarksText;
		_hasAfterArrive = _dataSource.HasAfterArriveText;
		_hasNextTrainButton = _dataSource.HasNextTrainId;

		RecalculateLayout();
		RaiseStateChanged();
	}

	private void RecalculateLayout()
	{
		// Phone-idiom row count (tablet idiom is handled by View using TimetableLayoutCalculator)
		int count = _rowCount + 1; // +1 for AfterRemarks
		if (_hasAfterArrive)
			count += 1;
		if (_hasNextTrainButton)
			count += 1;

		_currentState.RowDefinitionCount = Math.Max(0, count);
		_currentState.AfterArriveRowIndex = TimetableLayoutCalculator.CalculateAfterArriveRowIndex(_rowCount);
		_currentState.NextTrainButtonRowIndex = TimetableLayoutCalculator.CalculateNextTrainButtonRowIndex(_rowCount, _hasAfterArrive);
	}

	private void OnLocationMarkerSourceStateChanged(object? sender, VerticalPageStateChangedEventArgs e)
	{
		if ((e.Changed & VerticalPageStateSection.RowStates) == 0
			&& e.Changed != VerticalPageStateSection.All)
			return;

		var rowStates = _locationMarkerSource.RowStates;

		int markerRow = -1;
		TimetableLocationState markerState = TimetableLocationState.Undefined;
		foreach (var kvp in rowStates)
		{
			if (kvp.Value.LocationState != TimetableLocationState.Undefined)
			{
				markerRow = kvp.Key;
				markerState = kvp.Value.LocationState;
				break;
			}
		}

		bool newBoxVisible = markerState != TimetableLocationState.Undefined;
		bool newLineVisible = markerState == TimetableLocationState.RunningToNextStation;

		bool boxChanged = _currentState.Marker.IsBoxVisible != newBoxVisible;
		bool lineChanged = _currentState.Marker.IsLineVisible != newLineVisible;
		bool rowChanged = _currentState.Marker.MarkerRow != markerRow;

		if (!boxChanged && !lineChanged && !rowChanged)
			return;

		int prevMarkerRow = _currentState.Marker.MarkerRow;
		_currentState.Marker.IsBoxVisible = newBoxVisible;
		_currentState.Marker.IsLineVisible = newLineVisible;
		_currentState.Marker.MarkerRow = markerRow;
		RaiseStateChanged();

		if (newBoxVisible && markerRow >= 0 && prevMarkerRow != markerRow)
			ScrollRequested?.Invoke(this, markerRow);
	}

	private void OnMarkerTogglePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(IMarkerToggleController.IsToggled))
		{
			_currentState.IsMarkingMode = _markerToggle.IsToggled;
			RaiseStateChanged();
		}
	}

	private void RaiseStateChanged()
	{
		StateChanged?.Invoke(this, new VerticalTimetableViewStateChangedEventArgs());
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_markerToggle.PropertyChanged -= OnMarkerTogglePropertyChanged;
		_dataSource.RowsChanged -= OnDataSourceRowsChanged;
		_locationMarkerSource.StateChanged -= OnLocationMarkerSourceStateChanged;
	}
}
