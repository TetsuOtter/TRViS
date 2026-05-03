using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Presenter for the ViewHost page.
/// All business logic for the DTAC main page lives here.
/// Tab visibility, orientation, and wake lock are View responsibilities.
/// </summary>
public sealed class ViewHostPresenter : IDisposable
{
    private readonly IAppViewModelProvider _appViewModel;
    private readonly ITimeProvider _timeProvider;
    private readonly IUserAlertService _userAlerts;
    private readonly IDtacCrashLogger _crashLogger;

    private ViewHostPageState _currentState = new();
    private bool _disposed = false;

    public ViewHostPageState CurrentState => _currentState;

    public event EventHandler<ViewHostStateChangedEventArgs>? StateChanged;

    public ViewHostPresenter(
        IAppViewModelProvider appViewModel,
        ITimeProvider timeProvider,
        IUserAlertService userAlerts,
        IDtacCrashLogger crashLogger)
    {
        _appViewModel = appViewModel ?? throw new ArgumentNullException(nameof(appViewModel));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _userAlerts = userAlerts ?? throw new ArgumentNullException(nameof(userAlerts));
        _crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));

        _appViewModel.PropertyChanged += OnAppViewModelPropertyChanged;
        _timeProvider.TimeChanged += OnTimeChanged;

        ApplyInitialState();
    }

    private void ApplyInitialState()
    {
        _currentState.TitleText = _appViewModel.SelectedWork?.Name ?? string.Empty;
        _currentState.IsBgAppIconVisible = _appViewModel.IsBgAppIconVisible;
    }

    private void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAppViewModelProvider.SelectedWork):
                _currentState.TitleText = _appViewModel.SelectedWork?.Name ?? string.Empty;
                RaiseStateChanged(ViewHostStateSection.TitleText);
                break;
        }
    }

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
        _timeProvider.TimeChanged -= OnTimeChanged;
    }
}
