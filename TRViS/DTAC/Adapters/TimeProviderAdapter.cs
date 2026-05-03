using TRViS.Services;

using LogicITimeProvider = TRViS.DTAC.Logic.Abstractions.ITimeProvider;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that bridges LocationService.TimeChanged to ITimeProvider.
/// </summary>
internal class TimeProviderAdapter : LogicITimeProvider
{
    private readonly LocationService _locationService;

    public TimeProviderAdapter(LocationService locationService)
    {
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
        _locationService.TimeChanged += OnTimeChanged;
    }

    private void OnTimeChanged(object? sender, int totalSeconds)
    {
        TimeChanged?.Invoke(this, totalSeconds);
    }

    public event EventHandler<int>? TimeChanged;
}
