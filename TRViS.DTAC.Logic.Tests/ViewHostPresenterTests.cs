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
        private AppTheme _currentAppTheme = AppTheme.Dark;
        private bool _isBgAppIconVisible = true;

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

        public AppTheme CurrentAppTheme
        {
            get => _currentAppTheme;
            set
            {
                if (_currentAppTheme != value)
                {
                    _currentAppTheme = value;
                    CurrentAppThemeChanged?.Invoke(this, value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentAppTheme)));
                }
            }
        }

        public bool IsBgAppIconVisible
        {
            get => _isBgAppIconVisible;
            set
            {
                if (_isBgAppIconVisible != value)
                {
                    _isBgAppIconVisible = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBgAppIconVisible)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<AppTheme>? CurrentAppThemeChanged;
    }

    private class FakeTimeProvider : ITimeProvider
    {
        public event EventHandler<int>? TimeChanged;

        public void RaiseTimeChanged(int totalSeconds)
            => TimeChanged?.Invoke(this, totalSeconds);
    }

    private class FakeUserAlertService : IUserAlertService
    {
        public int AlertCount { get; private set; } = 0;
        public string? LastTitle { get; private set; }

        public void DisplayAlert(string title, string message, string cancel)
        {
            AlertCount++;
            LastTitle = title;
        }
    }

    private class FakeCrashLogger : IDtacCrashLogger
    {
        public int LogCount { get; private set; } = 0;
        public void Log(Exception ex, string? context = null) => LogCount++;
    }

    #endregion

    #region Helpers

    private static (
        ViewHostPresenter presenter,
        FakeAppViewModelProvider appViewModel,
        FakeTimeProvider timeProvider,
        FakeUserAlertService userAlerts,
        FakeCrashLogger crashLogger
    ) CreatePresenter()
    {
        var appViewModel = new FakeAppViewModelProvider();
        var timeProvider = new FakeTimeProvider();
        var userAlerts = new FakeUserAlertService();
        var crashLogger = new FakeCrashLogger();

        var presenter = new ViewHostPresenter(
            appViewModel,
            timeProvider,
            userAlerts,
            crashLogger);

        return (presenter, appViewModel, timeProvider, userAlerts, crashLogger);
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

    #region Theme Toggle Tests

    [Fact]
    public void OnChangeThemeButtonClicked_TogglesTheme_DarkToLight()
    {
        var (presenter, appViewModel, _, _, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Dark;
        presenter.OnChangeThemeButtonClicked();

        Assert.Equal(AppTheme.Light, appViewModel.CurrentAppTheme);
    }

    [Fact]
    public void OnChangeThemeButtonClicked_TogglesTheme_LightToDark()
    {
        var (presenter, appViewModel, _, _, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Light;
        presenter.OnChangeThemeButtonClicked();

        Assert.Equal(AppTheme.Dark, appViewModel.CurrentAppTheme);
    }

    #endregion

    #region BgAppIcon Toggle Tests

    [Fact]
    public void OnToggleBgAppIconRequested_LightTheme_ShowsAlert_DoesNotChange()
    {
        var (presenter, appViewModel, _, userAlerts, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Light;
        appViewModel.IsBgAppIconVisible = true;

        presenter.OnToggleBgAppIconRequested();

        Assert.Equal(1, userAlerts.AlertCount);
        Assert.True(appViewModel.IsBgAppIconVisible);
    }

    [Fact]
    public void OnToggleBgAppIconRequested_DarkTheme_TogglesNormally()
    {
        var (presenter, appViewModel, _, userAlerts, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Dark;
        appViewModel.IsBgAppIconVisible = true;

        presenter.OnToggleBgAppIconRequested();

        Assert.Equal(0, userAlerts.AlertCount);
        Assert.False(appViewModel.IsBgAppIconVisible);
    }

    [Fact]
    public void OnToggleBgAppIconRequested_DarkTheme_False_TogglesTo_True()
    {
        var (presenter, appViewModel, _, userAlerts, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Dark;
        appViewModel.IsBgAppIconVisible = false;

        presenter.OnToggleBgAppIconRequested();

        Assert.Equal(0, userAlerts.AlertCount);
        Assert.True(appViewModel.IsBgAppIconVisible);
    }

    #endregion

    #region AppViewModel State Propagation Tests

    [Fact]
    public void AppViewModel_SelectedWorkChanged_UpdatesTitleText()
    {
        var (presenter, appViewModel, _, _, _) = CreatePresenter();

        ViewHostStateChangedEventArgs? eventArgs = null;
        presenter.StateChanged += (_, e) => eventArgs = e;

        appViewModel.SelectedWork = MakeWork("My Work");

        Assert.Equal("My Work", presenter.CurrentState.TitleText);
        Assert.NotNull(eventArgs);
        Assert.True((eventArgs!.Changed & ViewHostStateSection.TitleText) != 0);
    }

    [Fact]
    public void AppViewModel_SelectedWorkGroupChanged_UpdatesWorkSpaceName()
    {
        var (presenter, appViewModel, _, _, _) = CreatePresenter();

        ViewHostStateChangedEventArgs? eventArgs = null;
        presenter.StateChanged += (_, e) => eventArgs = e;

        appViewModel.SelectedWorkGroup = MakeWorkGroup("My Group");

        Assert.Equal("My Group", presenter.CurrentState.WorkSpaceName);
        Assert.NotNull(eventArgs);
        Assert.True((eventArgs!.Changed & ViewHostStateSection.WorkSpaceName) != 0);
    }

    [Fact]
    public void AppViewModel_SelectedTrainChanged_UpdatesAffectDate()
    {
        var (presenter, appViewModel, _, _, _) = CreatePresenter();

        ViewHostStateChangedEventArgs? eventArgs = null;
        presenter.StateChanged += (_, e) => eventArgs = e;

        var trainData = MakeTrainData(affectDate: new DateOnly(2024, 3, 15));
        appViewModel.SelectedTrainData = trainData;

        Assert.Equal("2024年3月15日", presenter.CurrentState.AffectDateText);
        Assert.NotNull(eventArgs);
        Assert.True((eventArgs!.Changed & ViewHostStateSection.AffectDate) != 0);
    }

    #endregion

    #region Time Label Tests

    [Fact]
    public void TimeProvider_TimeChanged_UpdatesTimeLabelText()
    {
        var (presenter, _, timeProvider, _, _) = CreatePresenter();

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
        var (presenter, _, timeProvider, _, _) = CreatePresenter();

        timeProvider.RaiseTimeChanged(-65);

        Assert.Equal("-00:01:05", presenter.CurrentState.TimeLabelText);
    }

    [Fact]
    public void TimeProvider_TimeChanged_Zero_Formats_Correctly()
    {
        var (presenter, _, timeProvider, _, _) = CreatePresenter();

        timeProvider.RaiseTimeChanged(0);

        Assert.Equal("00:00:00", presenter.CurrentState.TimeLabelText);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_UnsubscribesEvents()
    {
        var (presenter, appViewModel, timeProvider, _, _) = CreatePresenter();

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
        var (presenter, _, _, _, _) = CreatePresenter();

        presenter.Dispose();

        var exception = Record.Exception(() => presenter.Dispose());
        Assert.Null(exception);
    }

    #endregion
}
