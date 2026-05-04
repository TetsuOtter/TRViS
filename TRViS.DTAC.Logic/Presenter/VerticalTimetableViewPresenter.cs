using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Lightweight presenter for <c>VerticalTimetableView</c>.
/// Handles layout calculations and marker/IsBusy state aggregation.
/// Row interaction (run/location service state machine) is handled by
/// <see cref="VerticalStylePagePresenter"/>; this presenter focuses only on
/// what is exclusive to the timetable grid widget.
/// </summary>
public sealed class VerticalTimetableViewPresenter : IDisposable
{
	private readonly IMarkerToggleController _markerToggle;
	private readonly IDtacCrashLogger _crashLogger;
	private readonly IVerticalTimetableDataSource _dataSource;

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
		IVerticalTimetableDataSource dataSource)
	{
		_markerToggle = markerToggle ?? throw new ArgumentNullException(nameof(markerToggle));
		_crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));
		_dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));

		_markerToggle.PropertyChanged += OnMarkerTogglePropertyChanged;
		_dataSource.RowsChanged += OnDataSourceRowsChanged;

		// Sync initial state
		_currentState.IsMarkingMode = _markerToggle.IsToggled;
	}

	// ---------- Intents from View ----------

	/// <summary>
	/// Call when AfterArriveText changes (null ↔ non-null).
	/// </summary>
	public void OnAfterArriveTextChanged(bool hasText)
	{
		_hasAfterArrive = hasText;
		RecalculateLayout();
		RaiseStateChanged();
	}

	/// <summary>
	/// Call when NextTrainId changes (null ↔ non-null).
	/// </summary>
	public void OnNextTrainIdChanged(bool hasId)
	{
		_hasNextTrainButton = hasId;
		RecalculateLayout();
		RaiseStateChanged();
	}

	/// <summary>
	/// Call when AfterRemarksText changes (null ↔ non-null).
	/// </summary>
	public void OnAfterRemarksTextChanged(bool hasText)
	{
		_hasAfterRemarks = hasText;
		RecalculateLayout();
		RaiseStateChanged();
	}

	/// <summary>
	/// Call when the location marker state changes.
	/// Updates <see cref="VerticalTimetableViewPageState.Marker"/> box/line visibility.
	/// </summary>
	public void OnLocationMarkerStateChanged(TimetableLocationState state)
	{
		_currentState.Marker.IsBoxVisible = state != TimetableLocationState.Undefined;
		_currentState.Marker.IsLineVisible = state == TimetableLocationState.RunningToNextStation;
		RaiseStateChanged();
	}

	/// <summary>
	/// Call when the location marker row position changes.
	/// Updates <see cref="VerticalTimetableViewPageState.Marker"/> row and fires
	/// <see cref="ScrollRequested"/> with the row index.
	/// </summary>
	public void OnLocationMarkerPositionChanged(int position)
	{
		_currentState.Marker.MarkerRow = position;
		RaiseStateChanged();
		ScrollRequested?.Invoke(this, position);
	}

	/// <summary>
	/// Sets the Logic-side busy state independently of the View's own busy state.
	/// The View manages its own busy flag; this is for Logic-driven operations.
	/// </summary>
	public void OnSetBusy(bool isBusy)
	{
		_currentState.IsBusy = isBusy;
		RaiseStateChanged();
	}

	/// <summary>
	/// Call when the marker toggle changes externally.
	/// </summary>
	public void OnMarkerToggleChanged(bool isToggled)
	{
		_currentState.IsMarkingMode = isToggled;
		RaiseStateChanged();
	}

	/// <summary>
	/// Logs an exception that occurred in View code via the crash logger adapter.
	/// </summary>
	public void LogException(Exception ex, string? context = null)
	{
		_crashLogger.Log(ex, context);
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

	private void OnMarkerTogglePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(IMarkerToggleController.IsToggled))
		{
			OnMarkerToggleChanged(_markerToggle.IsToggled);
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
	}
}
