using TRViS.Services;

using LogicITimeProvider = TRViS.DTAC.Logic.Abstractions.ITimeProvider;
using AppITimeProvider = TRViS.LocationService.Abstractions.ITimeProvider;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that bridges LocationService.TimeChanged to ITimeProvider.
/// </summary>
internal class TimeProviderAdapter : LogicITimeProvider, IDisposable
{
    private readonly TRViS.Services.LocationService _locationService;
    private readonly AppITimeProvider _appTimeProvider;
    private bool _disposed;

    public TimeProviderAdapter(TRViS.Services.LocationService locationService, AppITimeProvider appTimeProvider)
    {
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
        _appTimeProvider = appTimeProvider ?? throw new ArgumentNullException(nameof(appTimeProvider));
        _locationService.TimeChanged += OnTimeChanged;
    }

    private void OnTimeChanged(object? sender, int totalSeconds)
    {
        TimeChanged?.Invoke(this, totalSeconds);
    }

    public event EventHandler<int>? TimeChanged;

    // LocationService fires TimeChanged only when the value changes. If the clock
    // was frozen before this adapter was created (so lastTime_s already equals the
    // frozen value), no event would fire and the caller would never see the current
    // time. GetCurrentTimeSeconds() lets callers prime their initial state without
    // waiting for an event that may never arrive.
    public int GetCurrentTimeSeconds() => _appTimeProvider.GetCurrentTimeSeconds();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _locationService.TimeChanged -= OnTimeChanged;
    }
}
