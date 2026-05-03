using System.ComponentModel;
using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;

namespace TRViS.DTAC.Logic.Tests;

public class VerticalTimetableViewPresenterTests
{
	#region Fakes

	private class FakeMarkerToggle : IMarkerToggleController
	{
		private bool _isToggled = false;

		public bool IsToggled
		{
			get => _isToggled;
			set
			{
				if (_isToggled != value)
				{
					_isToggled = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsToggled)));
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public void ResetToggle()
		{
			IsToggled = false;
		}
	}

	private class FakeCrashLogger : IDtacCrashLogger
	{
		public List<(Exception ex, string? ctx)> Calls { get; } = [];

		public void Log(Exception ex, string? context = null)
			=> Calls.Add((ex, context));
	}

	private class FakeDataSource : IVerticalTimetableDataSource
	{
		private IReadOnlyList<bool> _isInfoRowList = [];
		private bool _hasAfterRemarksText;
		private bool _hasAfterArriveText;
		private bool _hasNextTrainId;

		public IReadOnlyList<bool> IsInfoRowList => _isInfoRowList;
		public bool HasAfterRemarksText => _hasAfterRemarksText;
		public bool HasAfterArriveText => _hasAfterArriveText;
		public bool HasNextTrainId => _hasNextTrainId;

		public event EventHandler? RowsChanged;

		public void SetRows(
			IReadOnlyList<bool> isInfoRowList,
			bool hasAfterRemarksText = false,
			bool hasAfterArriveText = false,
			bool hasNextTrainId = false)
		{
			_isInfoRowList = isInfoRowList;
			_hasAfterRemarksText = hasAfterRemarksText;
			_hasAfterArriveText = hasAfterArriveText;
			_hasNextTrainId = hasNextTrainId;
			RowsChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private static VerticalTimetableViewPresenter CreatePresenter(
		out FakeMarkerToggle markerToggle,
		out FakeCrashLogger crashLogger,
		out FakeDataSource dataSource)
	{
		markerToggle = new FakeMarkerToggle();
		crashLogger = new FakeCrashLogger();
		dataSource = new FakeDataSource();
		return new VerticalTimetableViewPresenter(markerToggle, crashLogger, dataSource);
	}

	private static VerticalTimetableViewPresenter CreatePresenter(
		out FakeMarkerToggle markerToggle,
		out FakeCrashLogger crashLogger)
		=> CreatePresenter(out markerToggle, out crashLogger, out _);

	#endregion

	// --- Initial state ---

	[Fact]
	public void InitialState_IsBusy_IsFalse()
	{
		var p = CreatePresenter(out _, out _);
		Assert.False(p.CurrentState.IsBusy);
	}

	[Fact]
	public void InitialState_IsMarkingMode_IsFalse()
	{
		var p = CreatePresenter(out _, out _);
		Assert.False(p.CurrentState.IsMarkingMode);
	}

	[Fact]
	public void InitialState_RowDefinitionCount_IsZero()
	{
		var p = CreatePresenter(out _, out _);
		Assert.Equal(0, p.CurrentState.RowDefinitionCount);
	}

	// --- RowsChanged (via data source) ---

	[Fact]
	public void RowsChanged_PhoneIdiom_3Rows_HasAfterArrive_CalculatesRowDefinitionCount()
	{
		var p = CreatePresenter(out _, out _, out var ds);
		// 3 rows + 1(AfterRemarks) + 1(AfterArrive) = 5
		ds.SetRows(
			isInfoRowList: new[] { false, true, false },
			hasAfterArriveText: true);

		Assert.Equal(5, p.CurrentState.RowDefinitionCount);
	}

	[Fact]
	public void RowsChanged_PhoneIdiom_SetsAfterArriveRowIndex()
	{
		var p = CreatePresenter(out _, out _, out var ds);
		ds.SetRows(
			isInfoRowList: new[] { false, false, false },
			hasAfterArriveText: true);

		// AfterArriveRowIndex = rowCount + 1 = 4
		Assert.Equal(4, p.CurrentState.AfterArriveRowIndex);
	}

	[Fact]
	public void RowsChanged_PhoneIdiom_WithAfterArrive_SetsNextTrainButtonRow()
	{
		var p = CreatePresenter(out _, out _, out var ds);
		ds.SetRows(
			isInfoRowList: new[] { false, false },
			hasAfterArriveText: true,
			hasNextTrainId: true);

		// NextTrainButtonRowIndex = rowCount + 2 = 4 (because hasAfterArrive)
		Assert.Equal(4, p.CurrentState.NextTrainButtonRowIndex);
	}

	[Fact]
	public void RowsChanged_RaisesStateChanged()
	{
		var p = CreatePresenter(out _, out _, out var ds);
		bool raised = false;
		p.StateChanged += (_, _) => raised = true;

		ds.SetRows(new[] { false });

		Assert.True(raised);
	}

	// --- OnAfterArriveTextChanged ---

	[Fact]
	public void OnAfterArriveTextChanged_RecalculatesRowIndex()
	{
		var p = CreatePresenter(out _, out _, out var ds);
		ds.SetRows(isInfoRowList: new[] { false, false });

		p.OnAfterArriveTextChanged(hasText: true);

		// rowCount=2 → AfterArriveRowIndex = 3, NextTrainButtonRowIndex = 4 (hasAfterArrive=true)
		Assert.Equal(3, p.CurrentState.AfterArriveRowIndex);
		Assert.Equal(4, p.CurrentState.NextTrainButtonRowIndex);
	}

	// --- OnLocationMarkerPositionChanged ---

	[Fact]
	public void OnLocationMarkerPositionChanged_FiresScrollRequested()
	{
		var p = CreatePresenter(out _, out _);
		int? receivedRow = null;
		p.ScrollRequested += (_, row) => receivedRow = row;

		p.OnLocationMarkerPositionChanged(3);

		Assert.NotNull(receivedRow);
		Assert.Equal(3, receivedRow!.Value);
	}

	[Fact]
	public void OnLocationMarkerPositionChanged_UpdatesMarkerRow()
	{
		var p = CreatePresenter(out _, out _);
		p.OnLocationMarkerPositionChanged(5);
		Assert.Equal(5, p.CurrentState.Marker.MarkerRow);
	}

	// --- OnLocationMarkerStateChanged ---

	[Fact]
	public void OnLocationMarkerStateChanged_AroundThisStation_UpdatesMarkerDisplay()
	{
		var p = CreatePresenter(out _, out _);
		p.OnLocationMarkerStateChanged(TimetableLocationState.AroundThisStation);

		Assert.True(p.CurrentState.Marker.IsBoxVisible);
		Assert.False(p.CurrentState.Marker.IsLineVisible);
	}

	[Fact]
	public void OnLocationMarkerStateChanged_RunningToNextStation_ShowsLineAndBox()
	{
		var p = CreatePresenter(out _, out _);
		p.OnLocationMarkerStateChanged(TimetableLocationState.RunningToNextStation);

		Assert.True(p.CurrentState.Marker.IsBoxVisible);
		Assert.True(p.CurrentState.Marker.IsLineVisible);
	}

	[Fact]
	public void OnLocationMarkerStateChanged_Undefined_HidesMarker()
	{
		var p = CreatePresenter(out _, out _);
		// First set to a visible state
		p.OnLocationMarkerStateChanged(TimetableLocationState.AroundThisStation);
		// Then reset to undefined
		p.OnLocationMarkerStateChanged(TimetableLocationState.Undefined);

		Assert.False(p.CurrentState.Marker.IsBoxVisible);
		Assert.False(p.CurrentState.Marker.IsLineVisible);
	}

	// --- OnSetBusy ---

	[Fact]
	public void OnSetBusy_True_FiresStateChanged()
	{
		var p = CreatePresenter(out _, out _);
		bool raised = false;
		p.StateChanged += (_, _) => raised = true;

		p.OnSetBusy(true);

		Assert.True(raised);
		Assert.True(p.CurrentState.IsBusy);
	}

	[Fact]
	public void OnSetBusy_False_SetsIsBusyFalse()
	{
		var p = CreatePresenter(out _, out _);
		p.OnSetBusy(true);
		p.OnSetBusy(false);

		Assert.False(p.CurrentState.IsBusy);
	}

	// --- OnMarkerToggleChanged ---

	[Fact]
	public void OnMarkerToggleChanged_UpdatesIsMarkingMode()
	{
		var p = CreatePresenter(out _, out _);
		p.OnMarkerToggleChanged(true);

		Assert.True(p.CurrentState.IsMarkingMode);
	}

	[Fact]
	public void MarkerTogglePropertyChange_PropagatesIsMarkingMode()
	{
		var p = CreatePresenter(out var markerToggle, out _);
		markerToggle.IsToggled = true;

		Assert.True(p.CurrentState.IsMarkingMode);
	}

	// --- LogException ---

	[Fact]
	public void LogException_DelegatesToCrashLogger()
	{
		var p = CreatePresenter(out _, out var logger);
		var ex = new InvalidOperationException("test");

		p.LogException(ex, "ctx");

		Assert.Single(logger.Calls);
		Assert.Same(ex, logger.Calls[0].ex);
		Assert.Equal("ctx", logger.Calls[0].ctx);
	}

	// --- Dispose ---

	[Fact]
	public void Dispose_UnsubscribesEvents()
	{
		var p = CreatePresenter(out var markerToggle, out _, out var ds);
		p.Dispose();

		// After dispose, property changes should NOT propagate
		bool raised = false;
		p.StateChanged += (_, _) => raised = true;

		markerToggle.IsToggled = true;
		ds.SetRows(new[] { false });

		Assert.False(raised);
	}
}
