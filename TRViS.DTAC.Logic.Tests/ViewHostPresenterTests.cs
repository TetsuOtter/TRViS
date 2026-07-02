using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.IO.Models;

using Xunit;

namespace TRViS.DTAC.Logic.Tests;

public class ViewHostPresenterTests
{
    #region Fake Implementations

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

        private string? _headerTimeFormat;
        public string? HeaderTimeFormat
        {
            get => _headerTimeFormat;
            set
            {
                if (_headerTimeFormat != value)
                {
                    _headerTimeFormat = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderTimeFormat)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private class FakeTimeProvider : ITimeProvider
    {
        public event EventHandler<int>? TimeChanged;

        public int CurrentTimeSeconds { get; set; }

        public int GetCurrentTimeSeconds() => CurrentTimeSeconds;

        public void RaiseTimeChanged(int totalSeconds)
            => TimeChanged?.Invoke(this, totalSeconds);
    }

    #endregion

    #region Helpers

    private static (
        ViewHostPresenter presenter,
        FakeAppViewModelProvider appViewModel,
        FakeTimeProvider timeProvider
    ) CreatePresenter()
    {
        var appViewModel = new FakeAppViewModelProvider();
        var timeProvider = new FakeTimeProvider();

        var presenter = new ViewHostPresenter(
            appViewModel,
            timeProvider);

        return (presenter, appViewModel, timeProvider);
    }

    private static WorkGroup MakeWorkGroup(string name) => new WorkGroup(
        Id: "wg1",
        Name: name
    );

    private static Work MakeWork(string name) => new Work(
        Id: "w1",
        WorkGroupId: "wg1",
        Name: name
    );

    private static TrainData MakeTrainData(int dayCount = 0, DateOnly? affectDate = null)
        => new TrainData(
            Id: "t1",
            Direction: Direction.Outbound,
            WorkName: "Test",
            TrainNumber: "101",
            Destination: "TestDest",
            DayCount: dayCount,
            AffectDate: affectDate,
            Rows: []
        );

    #endregion

    #region AppViewModel State Propagation Tests

    [Fact]
    public void AppViewModel_SelectedWorkChanged_UpdatesTitleText()
    {
        var (presenter, appViewModel, _) = CreatePresenter();

        ViewHostStateChangedEventArgs? eventArgs = null;
        presenter.StateChanged += (_, e) => eventArgs = e;

        appViewModel.SelectedWork = MakeWork("My Work");

        Assert.Equal("My Work", presenter.CurrentState.TitleText);
        Assert.NotNull(eventArgs);
        Assert.True((eventArgs!.Changed & ViewHostStateSection.TitleText) != 0);
    }

    #endregion

    #region Time Label Tests

    [Fact]
    public void TimeProvider_TimeChanged_UpdatesTimeLabelText()
    {
        var (presenter, _, timeProvider) = CreatePresenter();

        ViewHostStateChangedEventArgs? eventArgs = null;
        presenter.StateChanged += (_, e) => eventArgs = e;

        timeProvider.RaiseTimeChanged(3723);

        Assert.Equal("01:02:03", presenter.CurrentState.TimeLabelText);
        Assert.NotNull(eventArgs);
        Assert.True((eventArgs!.Changed & ViewHostStateSection.TimeLabel) != 0);
    }

    [Fact]
    public void TimeProvider_TimeChanged_Negative_FormatWithMinus()
    {
        var (presenter, _, timeProvider) = CreatePresenter();

        timeProvider.RaiseTimeChanged(-65);

        Assert.Equal("-00:01:05", presenter.CurrentState.TimeLabelText);
    }

    [Fact]
    public void TimeProvider_TimeChanged_Zero_Formats_Correctly()
    {
        var (presenter, _, timeProvider) = CreatePresenter();

        timeProvider.RaiseTimeChanged(0);

        Assert.Equal("00:00:00", presenter.CurrentState.TimeLabelText);
    }

    [Fact]
    public void HeaderTimeFormat_HHmm_ShortensTimeLabel()
    {
        var (presenter, appViewModel, timeProvider) = CreatePresenter();

        appViewModel.HeaderTimeFormat = "HH:mm";
        timeProvider.RaiseTimeChanged(3723);

        Assert.Equal("01:02", presenter.CurrentState.TimeLabelText);
    }

    [Fact]
    public void HeaderTimeFormat_ChangedAfterTimeReceived_RereformatsExistingTime()
    {
        var (presenter, appViewModel, timeProvider) = CreatePresenter();

        timeProvider.RaiseTimeChanged(3723);
        Assert.Equal("01:02:03", presenter.CurrentState.TimeLabelText);

        // フォーマット切り替え後、既存時刻が新フォーマットで再描画される
        appViewModel.HeaderTimeFormat = "HH:mm";
        Assert.Equal("01:02", presenter.CurrentState.TimeLabelText);

        // null に戻すと既定 (HH:mm:ss) に戻る
        appViewModel.HeaderTimeFormat = null;
        Assert.Equal("01:02:03", presenter.CurrentState.TimeLabelText);
    }

    [Fact]
    public void HeaderTimeFormat_UnknownFormat_FallsBackToDefault()
    {
        var (presenter, appViewModel, timeProvider) = CreatePresenter();

        appViewModel.HeaderTimeFormat = "completely-unknown";
        timeProvider.RaiseTimeChanged(3723);

        Assert.Equal("01:02:03", presenter.CurrentState.TimeLabelText);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_UnsubscribesEvents()
    {
        var (presenter, appViewModel, timeProvider) = CreatePresenter();

        var stateChangedCount = 0;
        presenter.StateChanged += (_, _) => stateChangedCount++;

        appViewModel.SelectedWork = MakeWork("Before Dispose");
        Assert.Equal(1, stateChangedCount);

        presenter.Dispose();

        appViewModel.SelectedWork = MakeWork("After Dispose");
        timeProvider.RaiseTimeChanged(999);

        Assert.Equal(1, stateChangedCount);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var (presenter, _, _) = CreatePresenter();

        presenter.Dispose();

        var exception = Record.Exception(() => presenter.Dispose());
        Assert.Null(exception);
    }

    #endregion
}
