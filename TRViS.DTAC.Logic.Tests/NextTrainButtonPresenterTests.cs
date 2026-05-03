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
		public TrainData? SelectedTrainData { get; set; }
		public event PropertyChangedEventHandler? PropertyChanged;
	}

	private class FakeCrashLogger : IDtacCrashLogger
	{
		public List<(Exception ex, string? context)> Calls { get; } = [];
		public void Log(Exception ex, string? context = null) => Calls.Add((ex, context));
	}

	private class FakeUserAlertService : IUserAlertService
	{
		public List<(string title, string message, string cancel)> Calls { get; } = [];
		public void DisplayAlert(string title, string message, string cancel) => Calls.Add((title, message, cancel));
	}

	private static NextTrainButtonPresenter CreatePresenter(
		out FakeTrainDataProvider trainData,
		out FakeCrashLogger crashLogger,
		out FakeUserAlertService alertService,
		out FakeAppViewModelProvider appVm)
	{
		trainData = new FakeTrainDataProvider();
		crashLogger = new FakeCrashLogger();
		alertService = new FakeUserAlertService();
		appVm = new FakeAppViewModelProvider();
		return new NextTrainButtonPresenter(trainData, appVm, crashLogger, alertService);
	}

	private static TrainData MakeTrainData(string id, string? trainNumber = "123A")
		=> new TrainData(id, Direction.Outbound, TrainNumber: trainNumber);

	#endregion

	// --- Initial state ---

	[Fact]
	public void InitialState_IsNotVisible()
	{
		var presenter = CreatePresenter(out _, out _, out _, out _);
		Assert.False(presenter.CurrentState.IsVisible);
	}

	[Fact]
	public void InitialState_ButtonText_IsEmpty()
	{
		var presenter = CreatePresenter(out _, out _, out _, out _);
		Assert.Equal(string.Empty, presenter.CurrentState.ButtonText);
	}

	[Fact]
	public void InitialState_CurrentNextTrainId_IsEmpty()
	{
		var presenter = CreatePresenter(out _, out _, out _, out _);
		Assert.Equal(string.Empty, presenter.CurrentState.CurrentNextTrainId);
	}

	// --- OnNextTrainIdChanged ---

	[Fact]
	public void OnNextTrainIdChanged_ValidTrain_SetsVisible()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);
		td.Add("T1", MakeTrainData("T1", "123A"));

		presenter.OnNextTrainIdChanged("T1");

		Assert.True(presenter.CurrentState.IsVisible);
	}

	[Fact]
	public void OnNextTrainIdChanged_ValidTrain_SetsButtonText()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);
		td.Add("T1", MakeTrainData("T1", "456B"));

		presenter.OnNextTrainIdChanged("T1");

		Assert.Contains("の時刻表へ", presenter.CurrentState.ButtonText);
	}

	[Fact]
	public void OnNextTrainIdChanged_ValidTrain_SetsCurrentNextTrainId()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);
		td.Add("T1", MakeTrainData("T1", "789C"));

		presenter.OnNextTrainIdChanged("T1");

		Assert.Equal("T1", presenter.CurrentState.CurrentNextTrainId);
	}

	[Fact]
	public void OnNextTrainIdChanged_ValidTrain_FiresStateChanged()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);
		td.Add("T1", MakeTrainData("T1", "123"));
		NextTrainButtonState? captured = null;
		presenter.StateChanged += (_, s) => captured = s;

		presenter.OnNextTrainIdChanged("T1");

		Assert.NotNull(captured);
		Assert.True(captured!.IsVisible);
	}

	[Fact]
	public void OnNextTrainIdChanged_GetTrainDataThrows_SetsNotVisible()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);
		td.SetThrowOnGet("T1", new InvalidOperationException("boom"));

		presenter.OnNextTrainIdChanged("T1");

		Assert.False(presenter.CurrentState.IsVisible);
	}

	[Fact]
	public void OnNextTrainIdChanged_GetTrainDataThrows_LogsCrash()
	{
		var presenter = CreatePresenter(out var td, out var logger, out _, out _);
		td.SetThrowOnGet("T1", new InvalidOperationException("boom"));

		presenter.OnNextTrainIdChanged("T1");

		Assert.Single(logger.Calls);
	}

	[Fact]
	public void OnNextTrainIdChanged_TrainDataNull_ThrowsKeyNotFoundException()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);
		td.Add("T1", null);

		Assert.Throws<KeyNotFoundException>(() => presenter.OnNextTrainIdChanged("T1"));
	}

	[Fact]
	public void OnNextTrainIdChanged_TrainNumberNull_ThrowsNullReferenceException()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);
		td.Add("T1", MakeTrainData("T1", null));

		Assert.Throws<NullReferenceException>(() => presenter.OnNextTrainIdChanged("T1"));
	}

	// --- OnButtonClicked ---

	[Fact]
	public void OnButtonClicked_EmptyNextTrainId_DoesNotCallSelectTrainData()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);

		presenter.OnButtonClicked();

		Assert.Equal(0, td.SelectCallCount);
	}

	[Fact]
	public void OnButtonClicked_ValidNextTrainId_CallsSelectTrainData()
	{
		var presenter = CreatePresenter(out var td, out _, out _, out _);
		td.Add("T1", MakeTrainData("T1", "123"));
		presenter.OnNextTrainIdChanged("T1");

		presenter.OnButtonClicked();

		Assert.Equal(1, td.SelectCallCount);
	}

	[Fact]
	public void OnButtonClicked_GetTrainDataThrows_ShowsAlert()
	{
		var presenter = CreatePresenter(out var td, out _, out var alerts, out _);
		td.Add("T1", MakeTrainData("T1", "123"));
		presenter.OnNextTrainIdChanged("T1");

		// Now make GetTrainData throw on the click
		td.SetThrowOnGet("T1", new InvalidOperationException("click error"));
		presenter.OnButtonClicked();

		Assert.Single(alerts.Calls);
		Assert.Equal("エラー", alerts.Calls[0].title);
	}

	[Fact]
	public void OnButtonClicked_GetTrainDataThrows_LogsCrash()
	{
		var presenter = CreatePresenter(out var td, out var logger, out _, out _);
		td.Add("T1", MakeTrainData("T1", "123"));
		presenter.OnNextTrainIdChanged("T1");
		td.SetThrowOnGet("T1", new InvalidOperationException("click error"));

		presenter.OnButtonClicked();

		// One from the click (setter succeeded, so no logger call from OnNextTrainIdChanged)
		Assert.Single(logger.Calls);
	}
}
