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
                    AppTheme old = _currentAppTheme;
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

    private class FakeViewHostModeProvider : IViewHostModeProvider
    {
        private bool _isViewHostVisible;
        private bool _isVerticalViewMode;
        private DTACTabMode _tabMode = DTACTabMode.None;

        public bool IsViewHostVisible
        {
            get => _isViewHostVisible;
            set
            {
                if (_isViewHostVisible != value)
                {
                    _isViewHostVisible = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsViewHostVisible)));
                }
            }
        }

        public bool IsVerticalViewMode
        {
            get => _isVerticalViewMode;
            set
            {
                if (_isVerticalViewMode != value)
                {
                    _isVerticalViewMode = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVerticalViewMode)));
                }
            }
        }

        public DTACTabMode TabMode
        {
            get => _tabMode;
            set
            {
                if (_tabMode != value)
                {
                    _tabMode = value;
                    _isVerticalViewMode = value == DTACTabMode.VerticalView;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TabMode)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVerticalViewMode)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHakoMode)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsWorkAffixMode)));
                }
            }
        }

        public bool IsHakoMode => _tabMode == DTACTabMode.Hako;
        public bool IsWorkAffixMode => _tabMode == DTACTabMode.WorkAffix;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private class FakeTimeProvider : ITimeProvider
    {
        public event EventHandler<int>? TimeChanged;

        public void RaiseTimeChanged(int totalSeconds)
            => TimeChanged?.Invoke(this, totalSeconds);
    }

    private class FakeEasterEgg : IEasterEggSettings
    {
        public bool KeepScreenOnWhenRunning { get; set; }
        public bool ShowMapWhenLandscape { get; set; }
#pragma warning disable CS0067 // Event is never used — interface requires it but tests don't fire it
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }

    private class FakeWakeLock : IWakeLockController
    {
        public bool IsWakeLockEnabled { get; private set; } = false;
        public int EnableCount { get; private set; } = 0;
        public int DisableCount { get; private set; } = 0;

        public void EnableWakeLock() { IsWakeLockEnabled = true; EnableCount++; }
        public void DisableWakeLock() { IsWakeLockEnabled = false; DisableCount++; }
    }

    private class FakeOrientationController : IOrientationController
    {
        public DesiredOrientation LastOrientation { get; private set; } = DesiredOrientation.All;
        public int SetCount { get; private set; } = 0;

        public void SetOrientation(DesiredOrientation orientation)
        {
            LastOrientation = orientation;
            SetCount++;
        }
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

    private class FakeNavigationSink : IViewHostNavigationSink
    {
        public bool? LastIsCurrentPage { get; private set; }
        public void NotifyNavigated(bool isCurrentPage) => LastIsCurrentPage = isCurrentPage;
    }

    #endregion

    #region Helpers

    private static (
        ViewHostPresenter presenter,
        FakeAppViewModelProvider appViewModel,
        FakeViewHostModeProvider viewHostMode,
        FakeTimeProvider timeProvider,
        FakeWakeLock wakeLock,
        FakeOrientationController orientation,
        FakeUserAlertService userAlerts,
        FakeCrashLogger crashLogger,
        FakeNavigationSink navigationSink
    ) CreatePresenter(bool isPhoneIdiom = false)
    {
        var appViewModel = new FakeAppViewModelProvider();
        var viewHostMode = new FakeViewHostModeProvider();
        var timeProvider = new FakeTimeProvider();
        var easterEgg = new FakeEasterEgg();
        var wakeLock = new FakeWakeLock();
        var orientation = new FakeOrientationController();
        var userAlerts = new FakeUserAlertService();
        var crashLogger = new FakeCrashLogger();
        var navigationSink = new FakeNavigationSink();

        var presenter = new ViewHostPresenter(
            appViewModel,
            viewHostMode,
            timeProvider,
            easterEgg,
            wakeLock,
            orientation,
            userAlerts,
            crashLogger,
            navigationSink);

        presenter.IsPhoneIdiom = isPhoneIdiom;

        return (presenter, appViewModel, viewHostMode, timeProvider, wakeLock, orientation, userAlerts, crashLogger, navigationSink);
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

    #region PhoneIdiom + Orientation Tests

    [Fact]
    public void IsPhoneIdiom_True_HakoMode_SetsPortraitOrientation()
    {
        var (presenter, _, viewHostMode, _, _, _, _, _, _) = CreatePresenter(isPhoneIdiom: false);

        viewHostMode.TabMode = DTACTabMode.Hako;
        presenter.IsPhoneIdiom = true;

        Assert.Equal(DesiredOrientation.Portrait, presenter.CurrentState.DesiredOrientation);
    }

    [Fact]
    public void IsPhoneIdiom_True_VerticalViewMode_SetsLandscapeOrientation()
    {
        var (presenter, _, viewHostMode, _, _, _, _, _, _) = CreatePresenter(isPhoneIdiom: false);

        viewHostMode.TabMode = DTACTabMode.VerticalView;
        presenter.IsPhoneIdiom = true;

        Assert.Equal(DesiredOrientation.Landscape, presenter.CurrentState.DesiredOrientation);
    }

    [Fact]
    public void IsPhoneIdiom_False_AnyMode_SetsAllOrientation()
    {
        var (presenter, _, viewHostMode, _, _, _, _, _, _) = CreatePresenter(isPhoneIdiom: false);

        viewHostMode.TabMode = DTACTabMode.Hako;

        Assert.Equal(DesiredOrientation.All, presenter.CurrentState.DesiredOrientation);
    }

    [Fact]
    public void OnViewAppearing_PhoneIdiom_HakoMode_SetsPortraitOrientation()
    {
        var (presenter, _, viewHostMode, _, _, orientation, _, _, _) = CreatePresenter(isPhoneIdiom: true);

        viewHostMode.TabMode = DTACTabMode.Hako;
        var setCountBefore = orientation.SetCount;

        presenter.OnViewAppearing();

        Assert.Equal(DesiredOrientation.Portrait, orientation.LastOrientation);
        Assert.True(orientation.SetCount > setCountBefore);
    }

    #endregion

    #region Wake Lock Tests

    [Fact]
    public void OnViewDisappearing_DisablesWakeLock_IfEnabled()
    {
        var (presenter, _, _, _, wakeLock, _, _, _, _) = CreatePresenter();

        // Manually enable wake lock
        wakeLock.EnableWakeLock();
        Assert.True(wakeLock.IsWakeLockEnabled);

        presenter.OnViewDisappearing();

        Assert.False(wakeLock.IsWakeLockEnabled);
        Assert.Equal(1, wakeLock.DisableCount);
    }

    [Fact]
    public void OnViewDisappearing_DoesNotDisableWakeLock_IfAlreadyDisabled()
    {
        var (presenter, _, _, _, wakeLock, _, _, _, _) = CreatePresenter();

        Assert.False(wakeLock.IsWakeLockEnabled);

        presenter.OnViewDisappearing();

        Assert.Equal(0, wakeLock.DisableCount);
    }

    #endregion

    #region Theme Toggle Tests

    [Fact]
    public void OnChangeThemeButtonClicked_TogglesTheme_DarkToLight()
    {
        var (presenter, appViewModel, _, _, _, _, _, _, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Dark;
        presenter.OnChangeThemeButtonClicked();

        Assert.Equal(AppTheme.Light, appViewModel.CurrentAppTheme);
    }

    [Fact]
    public void OnChangeThemeButtonClicked_TogglesTheme_LightToDark()
    {
        var (presenter, appViewModel, _, _, _, _, _, _, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Light;
        presenter.OnChangeThemeButtonClicked();

        Assert.Equal(AppTheme.Dark, appViewModel.CurrentAppTheme);
    }

    #endregion

    #region BgAppIcon Toggle Tests

    [Fact]
    public void OnToggleBgAppIconRequested_LightTheme_BgInvisible_ShowsAlert_DoesNotChange()
    {
        var (presenter, appViewModel, _, _, _, _, userAlerts, _, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Light;
        appViewModel.IsBgAppIconVisible = false; // start as not visible
        // Attempt to make it true -> false (toggle from false = try to set false would show alert?)
        // Actually: BgAppIconVisible = false means icon is NOT visible
        // Toggle: newState = !false = true -> that should be OK
        // Let's set it to true and try to hide it
        appViewModel.IsBgAppIconVisible = true;

        // Now toggle (attempt to set false in Light mode)
        presenter.OnToggleBgAppIconRequested();

        // Should show alert and NOT change
        Assert.Equal(1, userAlerts.AlertCount);
        Assert.True(appViewModel.IsBgAppIconVisible); // unchanged
    }

    [Fact]
    public void OnToggleBgAppIconRequested_DarkTheme_TogglesNormally()
    {
        var (presenter, appViewModel, _, _, _, _, userAlerts, _, _) = CreatePresenter();

        appViewModel.CurrentAppTheme = AppTheme.Dark;
        appViewModel.IsBgAppIconVisible = true;

        presenter.OnToggleBgAppIconRequested();

        Assert.Equal(0, userAlerts.AlertCount);
        Assert.False(appViewModel.IsBgAppIconVisible);
    }

    [Fact]
    public void OnToggleBgAppIconRequested_DarkTheme_False_TogglesTo_True()
    {
        var (presenter, appViewModel, _, _, _, _, userAlerts, _, _) = CreatePresenter();

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
        var (presenter, appViewModel, _, _, _, _, _, _, _) = CreatePresenter();

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
        var (presenter, appViewModel, _, _, _, _, _, _, _) = CreatePresenter();

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
        var (presenter, appViewModel, _, _, _, _, _, _, _) = CreatePresenter();

        ViewHostStateChangedEventArgs? eventArgs = null;
        presenter.StateChanged += (_, e) => eventArgs = e;

        var trainData = MakeTrainData(affectDate: new DateOnly(2024, 3, 15));
        appViewModel.SelectedTrainData = trainData;

        Assert.Equal("2024年3月15日", presenter.CurrentState.AffectDateText);
        Assert.NotNull(eventArgs);
        Assert.True((eventArgs!.Changed & ViewHostStateSection.AffectDate) != 0);
    }

    #endregion

    #region TabMode Visibility Tests

    [Fact]
    public void ViewHostMode_TabModeChanged_Hako_UpdatesVisibility()
    {
        var (presenter, _, viewHostMode, _, _, _, _, _, _) = CreatePresenter();

        ViewHostStateChangedEventArgs? eventArgs = null;
        presenter.StateChanged += (_, e) => eventArgs = e;

        viewHostMode.TabMode = DTACTabMode.Hako;

        Assert.True(presenter.CurrentState.IsHakoVisible);
        Assert.False(presenter.CurrentState.IsTimetableVisible);
        Assert.False(presenter.CurrentState.IsWorkAffixVisible);
        Assert.NotNull(eventArgs);
        Assert.True((eventArgs!.Changed & ViewHostStateSection.TabVisibility) != 0);
    }

    [Fact]
    public void ViewHostMode_TabModeChanged_VerticalView_UpdatesVisibility()
    {
        var (presenter, _, viewHostMode, _, _, _, _, _, _) = CreatePresenter();

        viewHostMode.TabMode = DTACTabMode.VerticalView;

        Assert.False(presenter.CurrentState.IsHakoVisible);
        Assert.True(presenter.CurrentState.IsTimetableVisible);
        Assert.False(presenter.CurrentState.IsWorkAffixVisible);
    }

    [Fact]
    public void ViewHostMode_TabModeChanged_WorkAffix_UpdatesVisibility()
    {
        var (presenter, _, viewHostMode, _, _, _, _, _, _) = CreatePresenter();

        viewHostMode.TabMode = DTACTabMode.WorkAffix;

        Assert.False(presenter.CurrentState.IsHakoVisible);
        Assert.False(presenter.CurrentState.IsTimetableVisible);
        Assert.True(presenter.CurrentState.IsWorkAffixVisible);
    }

    #endregion

    #region Time Label Tests

    [Fact]
    public void TimeProvider_TimeChanged_UpdatesTimeLabelText()
    {
        var (presenter, _, _, timeProvider, _, _, _, _, _) = CreatePresenter();

        ViewHostStateChangedEventArgs? eventArgs = null;
        presenter.StateChanged += (_, e) => eventArgs = e;

        // 1h 2m 3s = 3723
        timeProvider.RaiseTimeChanged(3723);

        Assert.Equal("01:02:03", presenter.CurrentState.TimeLabelText);
        Assert.NotNull(eventArgs);
        Assert.True((eventArgs!.Changed & ViewHostStateSection.TimeLabel) != 0);
    }

    [Fact]
    public void TimeProvider_TimeChanged_Negative_FormatWithMinus()
    {
        var (presenter, _, _, timeProvider, _, _, _, _, _) = CreatePresenter();

        timeProvider.RaiseTimeChanged(-65); // -1m5s

        Assert.Equal("-00:01:05", presenter.CurrentState.TimeLabelText);
    }

    [Fact]
    public void TimeProvider_TimeChanged_Zero_Formats_Correctly()
    {
        var (presenter, _, _, timeProvider, _, _, _, _, _) = CreatePresenter();

        timeProvider.RaiseTimeChanged(0);

        Assert.Equal("00:00:00", presenter.CurrentState.TimeLabelText);
    }

    #endregion

    #region Navigation Sink Tests

    [Fact]
    public void OnViewHostNavigatedTo_True_NotifiesSink()
    {
        var (presenter, _, _, _, _, _, _, _, navigationSink) = CreatePresenter();

        presenter.OnViewHostNavigatedTo(true);

        Assert.Equal(true, navigationSink.LastIsCurrentPage);
    }

    [Fact]
    public void OnViewHostNavigatedTo_False_NotifiesSink()
    {
        var (presenter, _, _, _, _, _, _, _, navigationSink) = CreatePresenter();

        presenter.OnViewHostNavigatedTo(false);

        Assert.Equal(false, navigationSink.LastIsCurrentPage);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_UnsubscribesEvents()
    {
        var (presenter, appViewModel, viewHostMode, timeProvider, _, _, _, _, _) = CreatePresenter();

        var stateChangedCount = 0;
        presenter.StateChanged += (_, _) => stateChangedCount++;

        appViewModel.SelectedWork = MakeWork("Before Dispose");
        Assert.Equal(1, stateChangedCount);

        presenter.Dispose();

        // After dispose, events should not fire StateChanged
        appViewModel.SelectedWork = MakeWork("After Dispose");
        viewHostMode.TabMode = DTACTabMode.WorkAffix;
        timeProvider.RaiseTimeChanged(999);

        Assert.Equal(1, stateChangedCount);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var (presenter, _, _, _, _, _, _, _, _) = CreatePresenter();

        presenter.Dispose();

        var exception = Record.Exception(() => presenter.Dispose());
        Assert.Null(exception);
    }

    #endregion
}
