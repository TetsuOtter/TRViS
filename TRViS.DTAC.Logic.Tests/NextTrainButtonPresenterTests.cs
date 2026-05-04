using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Tests;

public class NextTrainButtonPresenterTests
{
	#region Fakes / Stubs

	private class FakeTrainDataProvider : INextTrainDataProvider
	{
		private readonly Dictionary<string, TrainData?> _data = new();
		private TrainData? _selected;

		public TrainData? LastSelected => _selected;
		public int SelectCallCount { get; private set; }

		public void Add(string id, TrainData? data) => _data[id] = data;
		public void SetThrowOnGet(string id, Exception ex) => _throws[id] = ex;
		private readonly Dictionary<string, Exception> _throws = new();

		public TrainData? GetTrainData(string id)
		{
			if (_throws.TryGetValue(id, out var ex)) throw ex;
			_data.TryGetValue(id, out var result);
			return result;
		}

		public void SelectTrainData(TrainData? data)
		{
			_selected = data;
			SelectCallCount++;
		}
	}

	private class FakeAppViewModelProvider : IAppViewModelProvider
	{
		public WorkGroup? SelectedWorkGroup { get; set; }
		public Work? SelectedWork { get; set; }

		private TrainData? _selectedTrainData;
		public TrainData? SelectedTrainData
		{
			get => _selectedTrainData;
			set
			{
				_selectedTrainData = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTrainData)));
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
	}

	private class FakeCrashLogger : IDtacCrashLogger
	{
		public List<(Exception ex, string? context)> Calls { get; } = [];
		public void Log(Exception ex, string? context = null) => Calls.Add((ex, context));
	}

	private static NextTrainButtonPresenter CreatePresenter(
		out FakeTrainDataProvider trainData,
		out FakeCrashLogger crashLogger,
		out FakeAppViewModelProvider appVm)
	{
		trainData = new FakeTrainDataProvider();
		crashLogger = new FakeCrashLogger();
		appVm = new FakeAppViewModelProvider();
		return new NextTrainButtonPresenter(trainData, appVm, crashLogger);
	}

	private static TrainData MakeTrainData(string id, string? trainNumber = "123A")
		=> new TrainData(id, Direction.Outbound, TrainNumber: trainNumber);

	private static TrainData MakeSelectedTrain(string nextTrainId)
		=> new TrainData("selected", Direction.Outbound, TrainNumber: "selected") { NextTrainId = nextTrainId };

	#endregion

	// --- Initial state ---

	[Fact]
	public void InitialState_IsNotVisible()
	{
		var presenter = CreatePresenter(out _, out _, out _);
		Assert.False(presenter.CurrentState.IsVisible);
	}

	[Fact]
	public void InitialState_ButtonText_IsEmpty()
	{
		var presenter = CreatePresenter(out _, out _, out _);
		Assert.Equal(string.Empty, presenter.CurrentState.ButtonText);
	}

	[Fact]
	public void InitialState_CurrentNextTrainId_IsEmpty()
	{
		var presenter = CreatePresenter(out _, out _, out _);
		Assert.Equal(string.Empty, presenter.CurrentState.CurrentNextTrainId);
	}

	// --- SelectedTrainData change handling ---

	[Fact]
	public void SelectedTrainDataChanged_ValidNextTrain_SetsVisible()
	{
		var presenter = CreatePresenter(out var td, out _, out var appVm);
		td.Add("T1", MakeTrainData("T1", "123A"));

		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		Assert.True(presenter.CurrentState.IsVisible);
	}

	[Fact]
	public void SelectedTrainDataChanged_ValidNextTrain_SetsButtonText()
	{
		var presenter = CreatePresenter(out var td, out _, out var appVm);
		td.Add("T1", MakeTrainData("T1", "456B"));

		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		Assert.Contains("の時刻表へ", presenter.CurrentState.ButtonText);
	}

	[Fact]
	public void SelectedTrainDataChanged_ValidNextTrain_SetsCurrentNextTrainId()
	{
		var presenter = CreatePresenter(out var td, out _, out var appVm);
		td.Add("T1", MakeTrainData("T1", "789C"));

		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		Assert.Equal("T1", presenter.CurrentState.CurrentNextTrainId);
	}

	[Fact]
	public void SelectedTrainDataChanged_ValidNextTrain_FiresStateChanged()
	{
		var presenter = CreatePresenter(out var td, out _, out var appVm);
		td.Add("T1", MakeTrainData("T1", "123"));
		NextTrainButtonState? captured = null;
		presenter.StateChanged += (_, s) => captured = s;

		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		Assert.NotNull(captured);
		Assert.True(captured!.IsVisible);
	}

	[Fact]
	public void SelectedTrainDataChanged_NullNextTrainId_HidesButton()
	{
		var presenter = CreatePresenter(out _, out _, out var appVm);
		appVm.SelectedTrainData = new TrainData("selected", Direction.Outbound, TrainNumber: "selected");

		Assert.False(presenter.CurrentState.IsVisible);
	}

	[Fact]
	public void SelectedTrainDataChanged_GetTrainDataThrows_SetsNotVisible()
	{
		var presenter = CreatePresenter(out var td, out _, out var appVm);
		td.SetThrowOnGet("T1", new InvalidOperationException("boom"));

		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		Assert.False(presenter.CurrentState.IsVisible);
	}

	[Fact]
	public void SelectedTrainDataChanged_GetTrainDataThrows_LogsCrash()
	{
		var presenter = CreatePresenter(out var td, out var logger, out var appVm);
		td.SetThrowOnGet("T1", new InvalidOperationException("boom"));

		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		Assert.Single(logger.Calls);
	}

	[Fact]
	public void SelectedTrainDataChanged_TrainDataNull_HidesButtonAndLogsCrash()
	{
		var presenter = CreatePresenter(out var td, out var logger, out var appVm);
		td.Add("T1", null);

		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		Assert.False(presenter.CurrentState.IsVisible);
		Assert.Single(logger.Calls);
	}

	[Fact]
	public void SelectedTrainDataChanged_TrainNumberNull_HidesButtonAndLogsCrash()
	{
		var presenter = CreatePresenter(out var td, out var logger, out var appVm);
		td.Add("T1", MakeTrainData("T1", null));

		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		Assert.False(presenter.CurrentState.IsVisible);
		Assert.Single(logger.Calls);
	}

	// --- OnButtonClicked ---

	[Fact]
	public void OnButtonClicked_EmptyNextTrainId_DoesNotCallSelectTrainData()
	{
		var presenter = CreatePresenter(out var td, out _, out _);

		presenter.OnButtonClicked();

		Assert.Equal(0, td.SelectCallCount);
	}

	[Fact]
	public void OnButtonClicked_ValidNextTrainId_CallsSelectTrainData()
	{
		var presenter = CreatePresenter(out var td, out _, out var appVm);
		td.Add("T1", MakeTrainData("T1", "123"));
		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		presenter.OnButtonClicked();

		Assert.Equal(1, td.SelectCallCount);
	}

	[Fact]
	public void OnButtonClicked_GetTrainDataThrows_ThrowsUserAlertException()
	{
		var presenter = CreatePresenter(out var td, out _, out var appVm);
		td.Add("T1", MakeTrainData("T1", "123"));
		appVm.SelectedTrainData = MakeSelectedTrain("T1");

		// Now make GetTrainData throw on the click
		td.SetThrowOnGet("T1", new InvalidOperationException("click error"));

		var ex = Assert.Throws<UserAlertException>(() => presenter.OnButtonClicked());
		Assert.Equal("エラー", ex.Title);
	}

	[Fact]
	public void OnButtonClicked_GetTrainDataThrows_LogsCrash()
	{
		var presenter = CreatePresenter(out var td, out var logger, out var appVm);
		td.Add("T1", MakeTrainData("T1", "123"));
		appVm.SelectedTrainData = MakeSelectedTrain("T1");
		td.SetThrowOnGet("T1", new InvalidOperationException("click error"));

		Assert.Throws<UserAlertException>(() => presenter.OnButtonClicked());

		// One from the click (SelectedTrainData setter succeeded, so no logger call from that)
		Assert.Single(logger.Calls);
	}
}
