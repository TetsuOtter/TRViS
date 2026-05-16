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

		public void Toggle()
		{
			IsToggled = !IsToggled;
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

	private class FakeLocationMarkerSource : ILocationMarkerStateSource
	{
		private readonly Dictionary<int, VerticalTimetableRowState> _rowStates = new();
		public IReadOnlyDictionary<int, VerticalTimetableRowState> RowStates => _rowStates;

		public event EventHandler<VerticalPageStateChangedEventArgs>? StateChanged;

		public void SetMarker(int row, TimetableLocationState state)
		{
			foreach (var rs in _rowStates.Values)
				rs.LocationState = TimetableLocationState.Undefined;

			if (!_rowStates.ContainsKey(row))
				_rowStates[row] = new VerticalTimetableRowState();

			_rowStates[row].LocationState = state;
			StateChanged?.Invoke(this, new VerticalPageStateChangedEventArgs(VerticalPageStateSection.RowStates));
		}

		public void ClearMarker()
		{
			foreach (var rs in _rowStates.Values)
				rs.LocationState = TimetableLocationState.Undefined;
			StateChanged?.Invoke(this, new VerticalPageStateChangedEventArgs(VerticalPageStateSection.RowStates));
		}

		/// <summary>
		/// Seeds a marker WITHOUT raising StateChanged — simulates the page
		/// presenter having set the first-station marker inside its own ctor,
		/// before the view presenter is constructed and subscribes (so the
		/// edge-triggered StateChanged is gone by then).
		/// </summary>
		public void PreSeedMarker(int row, TimetableLocationState state)
		{
			if (!_rowStates.ContainsKey(row))
				_rowStates[row] = new VerticalTimetableRowState();
			_rowStates[row].LocationState = state;
		}
	}

	private static VerticalTimetableViewPresenter CreatePresenter(
		out FakeMarkerToggle markerToggle,
		out FakeCrashLogger crashLogger,
		out FakeDataSource dataSource,
		out FakeLocationMarkerSource markerSource)
	{
		markerToggle = new FakeMarkerToggle();
		crashLogger = new FakeCrashLogger();
		dataSource = new FakeDataSource();
		markerSource = new FakeLocationMarkerSource();
		return new VerticalTimetableViewPresenter(markerToggle, crashLogger, dataSource, markerSource);
	}

	private static VerticalTimetableViewPresenter CreatePresenter(
		out FakeMarkerToggle markerToggle,
		out FakeCrashLogger crashLogger,
		out FakeDataSource dataSource)
		=> CreatePresenter(out markerToggle, out crashLogger, out dataSource, out _);

	private static VerticalTimetableViewPresenter CreatePresenter(
		out FakeMarkerToggle markerToggle,
		out FakeCrashLogger crashLogger)
		=> CreatePresenter(out markerToggle, out crashLogger, out _, out _);

	#endregion

	// --- Initial state ---

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

	// --- Location marker via ILocationMarkerStateSource ---

	[Fact]
	public void LocationMarkerSource_PositionChange_FiresScrollRequested()
	{
		var p = CreatePresenter(out _, out _, out _, out var markerSource);
		int? receivedRow = null;
		p.ScrollRequested += (_, row) => receivedRow = row;

		markerSource.SetMarker(3, TimetableLocationState.AroundThisStation);

		Assert.NotNull(receivedRow);
		Assert.Equal(3, receivedRow!.Value);
	}

	[Fact]
	public void LocationMarkerSource_PositionChange_UpdatesMarkerRow()
	{
		var p = CreatePresenter(out _, out _, out _, out var markerSource);
		markerSource.SetMarker(5, TimetableLocationState.AroundThisStation);
		Assert.Equal(5, p.CurrentState.Marker.MarkerRow);
	}

	[Fact]
	public void LocationMarkerSource_AroundThisStation_UpdatesMarkerDisplay()
	{
		var p = CreatePresenter(out _, out _, out _, out var markerSource);
		markerSource.SetMarker(0, TimetableLocationState.AroundThisStation);

		Assert.True(p.CurrentState.Marker.IsBoxVisible);
		Assert.False(p.CurrentState.Marker.IsLineVisible);
	}

	[Fact]
	public void LocationMarkerSource_RunningToNextStation_ShowsLineAndBox()
	{
		var p = CreatePresenter(out _, out _, out _, out var markerSource);
		markerSource.SetMarker(0, TimetableLocationState.RunningToNextStation);

		Assert.True(p.CurrentState.Marker.IsBoxVisible);
		Assert.True(p.CurrentState.Marker.IsLineVisible);
	}

	// 回帰テスト (始発駅マーカー不具合): page presenter が自身の ctor で
	// 始発駅マーカーを RowStates に立てるのは、この view presenter が構築・
	// 購読するより前 (VerticalStylePage.xaml.cs:63 → :64)。エッジトリガの
	// StateChanged は購読時には消えているため、ctor で現在の RowStates レベルを
	// 補正しないと、運行開始時のマーカーが「発車して検出器が次の StateChanged を
	// 出すまで」表示されない。ctor 構築前に marker source を seed して再現する。
	[Fact]
	public void Ctor_ReconcilesPreExistingMarker_FromSource()
	{
		var markerToggle = new FakeMarkerToggle();
		var crashLogger = new FakeCrashLogger();
		var dataSource = new FakeDataSource();
		var markerSource = new FakeLocationMarkerSource();

		// page presenter ctor 相当: 購読前に始発駅マーカーが既に立っている
		markerSource.PreSeedMarker(1, TimetableLocationState.AroundThisStation);

		var p = new VerticalTimetableViewPresenter(markerToggle, crashLogger, dataSource, markerSource);

		Assert.True(p.CurrentState.Marker.IsBoxVisible,
			"ctor must level-reconcile the marker that was set before subscription");
		Assert.False(p.CurrentState.Marker.IsLineVisible);
		Assert.Equal(1, p.CurrentState.Marker.MarkerRow);
	}

	[Fact]
	public void Ctor_NoPreExistingMarker_MarkerStaysHidden()
	{
		var p = CreatePresenter(out _, out _, out _, out _);

		Assert.False(p.CurrentState.Marker.IsBoxVisible);
		Assert.False(p.CurrentState.Marker.IsLineVisible);
	}

	[Fact]
	public void LocationMarkerSource_ClearMarker_HidesMarker()
	{
		var p = CreatePresenter(out _, out _, out _, out var markerSource);
		markerSource.SetMarker(0, TimetableLocationState.AroundThisStation);
		markerSource.ClearMarker();

		Assert.False(p.CurrentState.Marker.IsBoxVisible);
		Assert.False(p.CurrentState.Marker.IsLineVisible);
	}

	// --- OnMarkerToggled ---

	[Fact]
	public void OnMarkerToggled_TogglesIsMarkingMode()
	{
		var p = CreatePresenter(out _, out _);
		Assert.False(p.CurrentState.IsMarkingMode);

		p.OnMarkerToggled();
		Assert.True(p.CurrentState.IsMarkingMode);

		p.OnMarkerToggled();
		Assert.False(p.CurrentState.IsMarkingMode);
	}

	[Fact]
	public void MarkerTogglePropertyChange_PropagatesIsMarkingMode()
	{
		var p = CreatePresenter(out var markerToggle, out _);
		markerToggle.IsToggled = true;

		Assert.True(p.CurrentState.IsMarkingMode);
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
