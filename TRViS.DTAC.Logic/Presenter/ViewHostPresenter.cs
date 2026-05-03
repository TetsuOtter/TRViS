using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Presenter for the ViewHost page.
/// All business logic for the DTAC main page lives here.
/// </summary>
public sealed class ViewHostPresenter : IDisposable
{
    private readonly IAppViewModelProvider _appViewModel;
    private readonly IViewHostModeProvider _viewHostMode;
    private readonly ITimeProvider _timeProvider;
    private readonly IEasterEggSettings _easterEgg;
    private readonly IWakeLockController _wakeLock;
    private readonly IOrientationController _orientation;
    private readonly IUserAlertService _userAlerts;
    private readonly IDtacCrashLogger _crashLogger;
    private readonly IViewHostNavigationSink? _navigationSink;

    private ViewHostPageState _currentState = new();
    private bool _disposed = false;

    /// <summary>
    /// Whether the device is a phone idiom.
    /// Set by the View (typically in constructor or OnAppearing).
    /// Changing this re-evaluates orientation.
    /// </summary>
    public bool IsPhoneIdiom
    {
        get => _isPhoneIdiom;
        set
        {
            if (_isPhoneIdiom == value)
                return;
            _isPhoneIdiom = value;
            UpdateOrientation();
            RaiseStateChanged(ViewHostStateSection.Orientation);
        }
    }
    private bool _isPhoneIdiom = false;

    public ViewHostPageState CurrentState => _currentState;

    public event EventHandler<ViewHostStateChangedEventArgs>? StateChanged;

    public ViewHostPresenter(
        IAppViewModelProvider appViewModel,
        IViewHostModeProvider viewHostMode,
        ITimeProvider timeProvider,
        IEasterEggSettings easterEgg,
        IWakeLockController wakeLock,
        IOrientationController orientation,
        IUserAlertService userAlerts,
        IDtacCrashLogger crashLogger,
        IViewHostNavigationSink? navigationSink = null)
    {
        _appViewModel = appViewModel ?? throw new ArgumentNullException(nameof(appViewModel));
        _viewHostMode = viewHostMode ?? throw new ArgumentNullException(nameof(viewHostMode));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _easterEgg = easterEgg ?? throw new ArgumentNullException(nameof(easterEgg));
        _wakeLock = wakeLock ?? throw new ArgumentNullException(nameof(wakeLock));
        _orientation = orientation ?? throw new ArgumentNullException(nameof(orientation));
        _userAlerts = userAlerts ?? throw new ArgumentNullException(nameof(userAlerts));
        _crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));
        _navigationSink = navigationSink;

        // Subscribe
        _appViewModel.PropertyChanged += OnAppViewModelPropertyChanged;
        _viewHostMode.PropertyChanged += OnViewHostModePropertyChanged;
        _timeProvider.TimeChanged += OnTimeChanged;

        // Apply initial state
        ApplyInitialState();
    }

    private void ApplyInitialState()
    {
        _currentState.TitleText = _appViewModel.SelectedWork?.Name ?? string.Empty;
        _currentState.WorkSpaceName = _appViewModel.SelectedWorkGroup?.Name ?? string.Empty;
        _currentState.AffectDateText = ComputeAffectDate(_appViewModel.SelectedTrainData);
        _currentState.IsBgAppIconVisible = _appViewModel.IsBgAppIconVisible;

        UpdateTabVisibility();
        UpdateOrientation();
    }

    // ---------- AppViewModel events ----------

    private void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAppViewModelProvider.SelectedWorkGroup):
                _currentState.WorkSpaceName = _appViewModel.SelectedWorkGroup?.Name ?? string.Empty;
                RaiseStateChanged(ViewHostStateSection.WorkSpaceName);
                break;

            case nameof(IAppViewModelProvider.SelectedWork):
                _currentState.TitleText = _appViewModel.SelectedWork?.Name ?? string.Empty;
                RaiseStateChanged(ViewHostStateSection.TitleText);
                break;

            case nameof(IAppViewModelProvider.SelectedTrainData):
                _currentState.AffectDateText = ComputeAffectDate(_appViewModel.SelectedTrainData);
                RaiseStateChanged(ViewHostStateSection.AffectDate);
                break;
        }
    }

    private static string ComputeAffectDate(TRViS.IO.Models.TrainData? trainData)
    {
        return ViewHostStateFactory.FormatAffectDateOnly(trainData?.AffectDate, trainData?.DayCount ?? 0);
    }

    // ---------- ViewHostMode events ----------

    private void OnViewHostModePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IViewHostModeProvider.IsVerticalViewMode)
            || e.PropertyName == nameof(IViewHostModeProvider.IsHakoMode)
            || e.PropertyName == nameof(IViewHostModeProvider.IsWorkAffixMode)
            || e.PropertyName == nameof(IViewHostModeProvider.TabMode))
        {
            UpdateTabVisibility();
            UpdateOrientation();
            RaiseStateChanged(ViewHostStateSection.TabVisibility | ViewHostStateSection.Orientation);
        }
    }

    private void UpdateTabVisibility()
    {
        _currentState.IsHakoVisible = _viewHostMode.IsHakoMode;
        _currentState.IsTimetableVisible = _viewHostMode.IsVerticalViewMode;
        _currentState.IsWorkAffixVisible = _viewHostMode.IsWorkAffixMode;
    }

    private void UpdateOrientation()
    {
        DesiredOrientation desired = !_isPhoneIdiom
            ? DesiredOrientation.All
            : _viewHostMode.TabMode switch
            {
                DTACTabMode.Hako => DesiredOrientation.Portrait,
                DTACTabMode.VerticalView => DesiredOrientation.Landscape,
                _ => DesiredOrientation.All,
            };

        if (_currentState.DesiredOrientation != desired)
        {
            _currentState.DesiredOrientation = desired;
            _orientation.SetOrientation(desired);
        }
    }

    // ---------- Time ----------

    private void OnTimeChanged(object? sender, int totalSeconds)
    {
        bool isMinus = totalSeconds < 0;
        int hour = Math.Abs(totalSeconds / 3600);
        int minute = Math.Abs((totalSeconds % 3600) / 60);
        int second = Math.Abs(totalSeconds % 60);

        string text = (isMinus ? "-" : string.Empty) + $"{hour:D2}:{minute:D2}:{second:D2}";
        _currentState.TimeLabelText = text;
        RaiseStateChanged(ViewHostStateSection.TimeLabel);
    }

    // ---------- Intents from View ----------

    /// <summary>
    /// Called when the ViewHost page is appearing.
    /// </summary>
    public void OnViewAppearing()
    {
        // Recompute orientation in case mode changed while page was hidden.
        // Then always apply it unconditionally so the OS enforces it on appear.
        UpdateOrientation();
        _orientation.SetOrientation(_currentState.DesiredOrientation);
    }

    /// <summary>
    /// Called when the ViewHost page is disappearing.
    /// </summary>
    public void OnViewDisappearing()
    {
        // Reset orientation
        if (_isPhoneIdiom)
        {
            _orientation.SetOrientation(DesiredOrientation.All);
        }

        // Disable wake lock
        if (_wakeLock.IsWakeLockEnabled)
        {
            _wakeLock.DisableWakeLock();
        }
    }

    /// <summary>
    /// Called when the change-theme button is clicked.
    /// Toggles between Light and Dark theme.
    /// </summary>
    public void OnChangeThemeButtonClicked()
    {
        AppTheme newTheme = _appViewModel.CurrentAppTheme == AppTheme.Dark
            ? AppTheme.Light
            : AppTheme.Dark;

        _appViewModel.CurrentAppTheme = newTheme;
    }

    /// <summary>
    /// Called when the toggle-BgAppIcon button is clicked.
    /// Applies business rule: cannot hide icon in Light theme.
    /// If not allowed, requests a user alert.
    /// </summary>
    public void OnToggleBgAppIconRequested()
    {
        bool newState = !_appViewModel.IsBgAppIconVisible;

        if (_appViewModel.CurrentAppTheme == AppTheme.Light && newState == false)
        {
            _userAlerts.DisplayAlert(
                "背景を非表示にできません",
                "現在のテーマがライトモードのため、背景アイコンは非表示にできません。",
                "OK");
            return;
        }

        _appViewModel.IsBgAppIconVisible = newState;
        _currentState.IsBgAppIconVisible = newState;
        RaiseStateChanged(ViewHostStateSection.BgAppIcon);
    }

    /// <summary>
    /// Called when Shell navigation fires. Propagates to the navigation sink (ViewHostModeAdapter).
    /// </summary>
    public void OnViewHostNavigatedTo(bool isCurrentPage)
    {
        _navigationSink?.NotifyNavigated(isCurrentPage);
    }

    // ---------- Helpers ----------

    private void RaiseStateChanged(ViewHostStateSection changed)
    {
        StateChanged?.Invoke(this, new ViewHostStateChangedEventArgs(changed));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _appViewModel.PropertyChanged -= OnAppViewModelPropertyChanged;
        _viewHostMode.PropertyChanged -= OnViewHostModePropertyChanged;
        _timeProvider.TimeChanged -= OnTimeChanged;
    }
}
