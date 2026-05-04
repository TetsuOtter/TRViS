using TRViS.Services;

using LogicITimeProvider = TRViS.DTAC.Logic.Abstractions.ITimeProvider;
using TRViS.LocationService.Abstractions;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that bridges LocationService.TimeChanged to ITimeProvider.
/// </summary>
internal class TimeProviderAdapter : LogicITimeProvider, IDisposable
{
    private readonly TRViS.Services.LocationService _locationService;
    private bool _disposed;

    public TimeProviderAdapter(TRViS.Services.LocationService locationService)
    {
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
        _locationService.TimeChanged += OnTimeChanged;
    }

    private void OnTimeChanged(object? sender, int totalSeconds)
    {
        TimeChanged?.Invoke(this, totalSeconds);
    }

    public event EventHandler<int>? TimeChanged;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _locationService.TimeChanged -= OnTimeChanged;
    }
}
