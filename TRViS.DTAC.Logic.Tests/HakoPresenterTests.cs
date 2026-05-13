using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.IO.Models;

using Xunit;

namespace TRViS.DTAC.Logic.Tests;

public class HakoPresenterTests
{
	#region Fake / Stub

	private class FakeAppViewModelProvider : IAppViewModelProvider
	{
		private WorkGroup? _selectedWorkGroup;
		private Work? _selectedWork;
		private TrainData? _selectedTrainData;

		public WorkGroup? SelectedWorkGroup
		{
			get => _selectedWorkGroup;
			set
			{
				if (!ReferenceEquals(_selectedWorkGroup, value))
				{
					_selectedWorkGroup = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedWorkGroup)));
				}
			}
		}

		public Work? SelectedWork
		{
			get => _selectedWork;
			set
			{
				if (!ReferenceEquals(_selectedWork, value))
				{
					_selectedWork = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedWork)));
				}
			}
		}

		public TrainData? SelectedTrainData
		{
			get => _selectedTrainData;
			set
			{
				if (!ReferenceEquals(_selectedTrainData, value))
				{
					_selectedTrainData = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTrainData)));
				}
			}
		}

		public string? HeaderTimeFormat { get; set; }

		public event PropertyChangedEventHandler? PropertyChanged;
	}

	private static (HakoPresenter presenter, FakeAppViewModelProvider appViewModel) CreatePresenter()
	{
		var appViewModel = new FakeAppViewModelProvider();
		return (new HakoPresenter(appViewModel), appViewModel);
	}

	private static Work MakeWork(string name) => new Work(Id: "w1", WorkGroupId: "wg1", Name: name);
	private static Work MakeWorkWithAffectDateText(string name, string affectDateText)
		=> new Work(Id: "w1", WorkGroupId: "wg1", Name: name, AffectDateText: affectDateText);
	private static WorkGroup MakeWorkGroup(string name) => new WorkGroup(Id: "wg1", Name: name);
	private static TrainData MakeTrainData(DateOnly? affectDate = null, int dayCount = 0)
		=> new TrainData(
			Id: "t1",
			Direction: Direction.Outbound,
			WorkName: "Test",
			TrainNumber: "101",
			Destination: "TestDest",
			DayCount: dayCount,
			AffectDate: affectDate,
			Rows: []);

	#endregion

	// --- constructor / initial state ---

	[Fact]
	public void InitialState_WorkInfoText_IsEmpty()
	{
		var (presenter, _) = CreatePresenter();
		Assert.Equal(string.Empty, presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void InitialState_AffectDateText_HasPrefixAndTodayDate()
	{
		// No train selected -> FormatAffectDateOnly(null, 0) returns today's date
		var (presenter, _) = CreatePresenter();
		string expectedDate = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy年M月d日");
		Assert.Equal(HakoPresenter.AffectDateLabelTextPrefix + expectedDate, presenter.CurrentState.AffectDateText);
	}

	// --- SelectedTrainData changes ---

	[Fact]
	public void SelectedTrainDataChanged_WithAffectDate_SetsFormattedText()
	{
		var (presenter, appViewModel) = CreatePresenter();
		appViewModel.SelectedTrainData = MakeTrainData(affectDate: new DateOnly(2025, 5, 3));

		Assert.Equal(HakoPresenter.AffectDateLabelTextPrefix + "2025年5月3日", presenter.CurrentState.AffectDateText);
	}

	[Fact]
	public void SelectedTrainDataChanged_WithDayCount_CalculatesDate()
	{
		var (presenter, appViewModel) = CreatePresenter();
		appViewModel.SelectedTrainData = MakeTrainData(dayCount: 2);

		string expectedDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-2).ToString("yyyy年M月d日");
		Assert.Equal(HakoPresenter.AffectDateLabelTextPrefix + expectedDate, presenter.CurrentState.AffectDateText);
	}

	[Fact]
	public void SelectedTrainDataChanged_RaisesStateChanged_AffectDate()
	{
		var (presenter, appViewModel) = CreatePresenter();
		HakoStateChangedEventArgs? args = null;
		presenter.StateChanged += (_, e) => args = e;

		appViewModel.SelectedTrainData = MakeTrainData(affectDate: new DateOnly(2025, 5, 3));

		Assert.NotNull(args);
		Assert.True(args.Changed.HasFlag(HakoStateSection.AffectDate));
	}

	// --- SelectedWork changes ---

	[Fact]
	public void SelectedWorkChanged_SetsWorkInfoText()
	{
		var (presenter, appViewModel) = CreatePresenter();
		appViewModel.SelectedWork = MakeWork("列車A");

		Assert.Equal("列車A\n", presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void SelectedWorkChanged_RaisesStateChanged_WorkInfo()
	{
		var (presenter, appViewModel) = CreatePresenter();
		HakoStateChangedEventArgs? args = null;
		presenter.StateChanged += (_, e) => args = e;

		appViewModel.SelectedWork = MakeWork("列車A");

		Assert.NotNull(args);
		Assert.True(args.Changed.HasFlag(HakoStateSection.WorkInfo));
	}

	// --- SelectedWorkGroup changes ---

	[Fact]
	public void SelectedWorkGroupChanged_SetsWorkInfoText()
	{
		var (presenter, appViewModel) = CreatePresenter();
		appViewModel.SelectedWorkGroup = MakeWorkGroup("東京区");

		Assert.Equal("\n東京区", presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void WorkInfo_BothWorkAndGroup_CombinedWithNewline()
	{
		var (presenter, appViewModel) = CreatePresenter();
		appViewModel.SelectedWork = MakeWork("列車A");
		appViewModel.SelectedWorkGroup = MakeWorkGroup("東京区");

		Assert.Equal("列車A\n東京区", presenter.CurrentState.WorkInfoText);
	}

	[Fact]
	public void WorkInfo_WorkClearedAfterBeingSet_ResetsToGroup()
	{
		var (presenter, appViewModel) = CreatePresenter();
		appViewModel.SelectedWork = MakeWork("列車A");
		appViewModel.SelectedWorkGroup = MakeWorkGroup("東京区");
		appViewModel.SelectedWork = null;

		Assert.Equal("\n東京区", presenter.CurrentState.WorkInfoText);
	}

	// --- prefix constant ---

	[Fact]
	public void AffectDateLabelTextPrefix_HasExpectedValue()
	{
		Assert.Equal("行路施行日\n", HakoPresenter.AffectDateLabelTextPrefix);
	}

	// --- AffectDateText override (任意の文字列) ---

	[Fact]
	public void SelectedWork_WithAffectDateText_DisplaysOverrideText()
	{
		var (presenter, appViewModel) = CreatePresenter();
		appViewModel.SelectedWork = MakeWorkWithAffectDateText("列車A", "ダイヤA");

		Assert.Equal(HakoPresenter.AffectDateLabelTextPrefix + "ダイヤA",
			presenter.CurrentState.AffectDateText);
	}

	[Fact]
	public void SelectedWork_WithAffectDateText_OverridesTrainAffectDate()
	{
		var (presenter, appViewModel) = CreatePresenter();
		appViewModel.SelectedWork = MakeWorkWithAffectDateText("列車A", "緊急ダイヤ");
		appViewModel.SelectedTrainData = MakeTrainData(affectDate: new DateOnly(2025, 5, 3));

		// Work.AffectDateText が優先され、TrainData.AffectDate の日付は使われない
		Assert.Equal(HakoPresenter.AffectDateLabelTextPrefix + "緊急ダイヤ",
			presenter.CurrentState.AffectDateText);
	}

	// --- Dispose ---

	[Fact]
	public void Dispose_UnsubscribesFromAppViewModel()
	{
		var (presenter, appViewModel) = CreatePresenter();

		int stateChangedCount = 0;
		presenter.StateChanged += (_, _) => stateChangedCount++;

		appViewModel.SelectedWork = MakeWork("Before");
		Assert.Equal(1, stateChangedCount);

		presenter.Dispose();

		appViewModel.SelectedWork = MakeWork("After");
		appViewModel.SelectedTrainData = MakeTrainData(affectDate: new DateOnly(2025, 1, 1));

		Assert.Equal(1, stateChangedCount);
	}

	[Fact]
	public void Dispose_CalledTwice_DoesNotThrow()
	{
		var (presenter, _) = CreatePresenter();
		presenter.Dispose();
		var exception = Record.Exception(() => presenter.Dispose());
		Assert.Null(exception);
	}
}
