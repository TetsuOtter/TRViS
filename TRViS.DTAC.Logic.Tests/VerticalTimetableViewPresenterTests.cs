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

	private static VerticalTimetableViewPresenter CreatePresenter(
		out FakeMarkerToggle markerToggle,
		out FakeCrashLogger crashLogger)
	{
		markerToggle = new FakeMarkerToggle();
		crashLogger = new FakeCrashLogger();
		return new VerticalTimetableViewPresenter(markerToggle, crashLogger);
	}

	#endregion

	private const double RowHeight = 65.0;

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

	// --- OnRowsChanged – phone idiom ---

	[Fact]
	public void OnRowsChanged_PhoneIdiom_3Rows_HasAfterArrive_CalculatesRowDefinitionCount()
	{
		var p = CreatePresenter(out _, out _);
		// 3 rows + 1(AfterRemarks) + 1(AfterArrive) = 5
		p.OnRowsChanged(
			isInfoRowList: new[] { false, true, false },
			hasAfterRemarksText: false,
			hasAfterArriveText: true,
			hasNextTrainId: false,
			isPhoneIdiom: true,
			scrollViewHeight: 0);

		Assert.Equal(5, p.CurrentState.RowDefinitionCount);
	}

	[Fact]
	public void OnRowsChanged_PhoneIdiom_SetsAfterArriveRowIndex()
	{
		var p = CreatePresenter(out _, out _);
		p.OnRowsChanged(
			isInfoRowList: new[] { false, false, false },
			hasAfterRemarksText: false,
			hasAfterArriveText: true,
			hasNextTrainId: false,
			isPhoneIdiom: true,
			scrollViewHeight: 0);

		// AfterArriveRowIndex = rowCount + 1 = 4
		Assert.Equal(4, p.CurrentState.AfterArriveRowIndex);
	}

	[Fact]
	public void OnRowsChanged_PhoneIdiom_WithAfterArrive_SetsNextTrainButtonRow()
	{
		var p = CreatePresenter(out _, out _);
		p.OnRowsChanged(
			isInfoRowList: new[] { false, false },
			hasAfterRemarksText: false,
			hasAfterArriveText: true,
			hasNextTrainId: true,
			isPhoneIdiom: true,
			scrollViewHeight: 0);

		// NextTrainButtonRowIndex = rowCount + 2 = 4 (because hasAfterArrive)
		Assert.Equal(4, p.CurrentState.NextTrainButtonRowIndex);
	}

	[Fact]
	public void OnRowsChanged_RaisesStateChanged()
	{
		var p = CreatePresenter(out _, out _);
		bool raised = false;
		p.StateChanged += (_, _) => raised = true;

		p.OnRowsChanged(new[] { false }, false, false, false, true, 0);

		Assert.True(raised);
	}

	// --- OnAfterArriveTextChanged ---

	[Fact]
	public void OnAfterArriveTextChanged_RecalculatesRowIndex()
	{
		var p = CreatePresenter(out _, out _);
		p.OnRowsChanged(
			isInfoRowList: new[] { false, false },
			hasAfterRemarksText: false,
			hasAfterArriveText: false,
			hasNextTrainId: false,
			isPhoneIdiom: true,
			scrollViewHeight: 0);

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
		double? receivedY = null;
		p.ScrollRequested += (_, y) => receivedY = y;

		p.OnLocationMarkerPositionChanged(3, RowHeight);

		// (3-1)*65 = 130
		Assert.NotNull(receivedY);
		Assert.Equal(130.0, receivedY!.Value, precision: 3);
	}

	[Fact]
	public void OnLocationMarkerPositionChanged_UpdatesMarkerRow()
	{
		var p = CreatePresenter(out _, out _);
		p.OnLocationMarkerPositionChanged(5, RowHeight);
		Assert.Equal(5, p.CurrentState.Marker.MarkerRow);
	}

	// --- OnLocationMarkerStateChanged ---

	[Fact]
	public void OnLocationMarkerStateChanged_AroundThisStation_UpdatesMarkerDisplay()
	{
		var p = CreatePresenter(out _, out _);
		p.OnLocationMarkerStateChanged(TimetableLocationState.AroundThisStation, RowHeight);

		Assert.True(p.CurrentState.Marker.IsBoxVisible);
		Assert.False(p.CurrentState.Marker.IsLineVisible);
		Assert.Equal(0.0, p.CurrentState.Marker.BoxMarginTop);
	}

	[Fact]
	public void OnLocationMarkerStateChanged_RunningToNextStation_HalfRowOffset()
	{
		var p = CreatePresenter(out _, out _);
		p.OnLocationMarkerStateChanged(TimetableLocationState.RunningToNextStation, RowHeight);

		Assert.True(p.CurrentState.Marker.IsBoxVisible);
		Assert.True(p.CurrentState.Marker.IsLineVisible);
		Assert.Equal(-(RowHeight / 2), p.CurrentState.Marker.BoxMarginTop, precision: 3);
	}

	[Fact]
	public void OnLocationMarkerStateChanged_Undefined_HidesMarker()
	{
		var p = CreatePresenter(out _, out _);
		// First set to a visible state
		p.OnLocationMarkerStateChanged(TimetableLocationState.AroundThisStation, RowHeight);
		// Then reset to undefined
		p.OnLocationMarkerStateChanged(TimetableLocationState.Undefined, RowHeight);

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
		var p = CreatePresenter(out var markerToggle, out _);
		p.Dispose();

		// After dispose, property changes should NOT propagate
		bool raised = false;
		p.StateChanged += (_, _) => raised = true;

		markerToggle.IsToggled = true;

		Assert.False(raised);
	}
}
