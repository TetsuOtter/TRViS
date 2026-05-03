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

	// Persisted layout inputs so we can re-compute on partial changes
	private int _rowCount = 0;
	private bool _hasAfterRemarks = false;
	private bool _hasAfterArrive = false;
	private bool _hasNextTrainButton = false;
	private bool _isPhoneIdiom = true;
	private double _scrollViewHeight = 0;

	private VerticalTimetableViewPageState _currentState = new();
	private bool _disposed = false;

	/// <summary>Current aggregate state (never null).</summary>
	public VerticalTimetableViewPageState CurrentState => _currentState;

	/// <summary>Raised whenever any part of <see cref="CurrentState"/> changes.</summary>
	public event EventHandler<VerticalTimetableViewStateChangedEventArgs>? StateChanged;

	/// <summary>
	/// Raised when the view should scroll to a specific Y position.
	/// </summary>
	public event EventHandler<double>? ScrollRequested;

	public VerticalTimetableViewPresenter(
		IMarkerToggleController markerToggle,
		IDtacCrashLogger crashLogger)
	{
		_markerToggle = markerToggle ?? throw new ArgumentNullException(nameof(markerToggle));
		_crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));

		_markerToggle.PropertyChanged += OnMarkerTogglePropertyChanged;

		// Sync initial state
		_currentState.IsMarkingMode = _markerToggle.IsToggled;
	}

	// ---------- Intents from View ----------

	/// <summary>
	/// Call when the row collection or any layout-affecting property changes.
	/// Re-computes RowDefinitionCount, GridHeightRequest, and row indices.
	/// </summary>
	public void OnRowsChanged(
		IReadOnlyList<bool> isInfoRowList,
		bool hasAfterRemarksText,
		bool hasAfterArriveText,
		bool hasNextTrainId,
		bool isPhoneIdiom,
		double scrollViewHeight)
	{
		_rowCount = isInfoRowList?.Count ?? 0;
		_hasAfterRemarks = hasAfterRemarksText;
		_hasAfterArrive = hasAfterArriveText;
		_hasNextTrainButton = hasNextTrainId;
		_isPhoneIdiom = isPhoneIdiom;
		_scrollViewHeight = scrollViewHeight;

		RecalculateLayout();
		RaiseStateChanged();
	}

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
	/// Call when the ScrollView height changes (tablet idiom re-layout).
	/// </summary>
	public void OnScrollViewHeightChanged(double height, bool isPhoneIdiom)
	{
		_scrollViewHeight = height;
		_isPhoneIdiom = isPhoneIdiom;
		RecalculateLayout();
		RaiseStateChanged();
	}

	/// <summary>
	/// Call when the location marker state changes.
	/// Updates <see cref="VerticalTimetableViewPageState.Marker"/> box/line visibility.
	/// </summary>
	public void OnLocationMarkerStateChanged(TimetableLocationState state, double rowHeight)
	{
		var display = TimetableLayoutCalculator.CalculateLocationMarkerDisplay(state, rowHeight);
		_currentState.Marker.IsBoxVisible = display.IsBoxVisible;
		_currentState.Marker.IsLineVisible = display.IsLineVisible;
		_currentState.Marker.BoxMarginTop = display.BoxMarginTop;
		RaiseStateChanged();
	}

	/// <summary>
	/// Call when the location marker row position changes.
	/// Updates <see cref="VerticalTimetableViewPageState.Marker"/> row and fires
	/// <see cref="ScrollRequested"/>.
	/// </summary>
	public void OnLocationMarkerPositionChanged(int position, double rowHeight)
	{
		_currentState.Marker.MarkerRow = position;

		double scrollY = TimetableLayoutCalculator.CalculateScrollTargetY(position, rowHeight);
		RaiseStateChanged();
		ScrollRequested?.Invoke(this, scrollY);
	}

	/// <summary>
	/// Call when the view's IsBusy state changes.
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

	private void RecalculateLayout()
	{
		const double rowHeight = 65; // matches VerticalTimetableView.RowHeight

		int rowDefCount = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			_rowCount,
			_hasAfterRemarks,
			_hasAfterArrive,
			_hasNextTrainButton,
			_isPhoneIdiom,
			_scrollViewHeight,
			rowHeight);

		_currentState.RowDefinitionCount = rowDefCount;
		_currentState.GridHeightRequest = TimetableLayoutCalculator.CalculateGridHeightRequest(rowDefCount, rowHeight);
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
	}
}
