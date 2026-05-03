using TRViS.DTAC.Logic.Abstractions;
using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps LocationService to implement IDtacLocationServiceController.
/// </summary>
internal class LocationServiceAdapter : IDtacLocationServiceController
{
	private readonly LocationService _locationService;

	public LocationServiceAdapter(LocationService locationService)
	{
		_locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));

		// Bridge GPS location updates
		_locationService.OnGpsLocationUpdated += OnGpsLocationUpdatedInternal;
	}

	private void OnGpsLocationUpdatedInternal(object? sender, Location? location)
	{
		if (location is null)
			return;

		GpsLocationUpdated?.Invoke(this, new GpsLocationUpdate(
			location.Latitude,
			location.Longitude,
			location.Accuracy));
	}

	public bool IsEnabled
	{
		get => _locationService.IsEnabled;
		set => _locationService.IsEnabled = value;
	}

	public bool CanUseService => _locationService.CanUseService;

	public bool NetworkSyncServiceCanStart => _locationService.NetworkSyncServiceCanStart;

	public StaLocationInfo[]? StaLocationInfo
	{
		get => _locationService.CurrentService?.StaLocationInfo;
		set
		{
			if (_locationService.CurrentService is not null)
				_locationService.CurrentService.StaLocationInfo = value;
		}
	}

	public int CurrentStationIndex => _locationService.CurrentService?.CurrentStationIndex ?? -1;

	public bool IsRunningToNextStation => _locationService.CurrentService?.IsRunningToNextStation ?? false;

	public event EventHandler<bool>? CanUseServiceChanged
	{
		add => _locationService.CanUseServiceChanged += value;
		remove => _locationService.CanUseServiceChanged -= value;
	}

	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged
	{
		add => _locationService.LocationStateChanged += value;
		remove => _locationService.LocationStateChanged -= value;
	}

	public event EventHandler<GpsLocationUpdate>? GpsLocationUpdated;

	public void ResetLocationInfo() => _locationService.CurrentService?.ResetLocationInfo();

	public void ForceSetLocationInfo(int stationIndex, bool isRunningToNextStation)
		=> _locationService.ForceSetLocationInfo(stationIndex, isRunningToNextStation);

	public void SetTimetableRows(TimetableRow[]? rows)
		=> _locationService.SetTimetableRows(rows);
}
