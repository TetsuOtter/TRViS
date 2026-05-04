using TRViS.DTAC.Logic.Abstractions;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.LocationService.Abstractions;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps LocationService to implement IDtacLocationServiceController.
/// </summary>
internal class LocationServiceAdapter : IDtacLocationServiceController
{
	private readonly TRViS.Services.LocationService _locationService;

	public LocationServiceAdapter(TRViS.Services.LocationService locationService)
	{
		_locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));

		// Bridge GPS location updates
		_locationService.OnGpsLocationUpdated += OnGpsLocationUpdatedInternal;
	}

	private void OnGpsLocationUpdatedInternal(object? sender, (double Longitude, double Latitude, double? Accuracy) location)
	{
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

	public event EventHandler<Exception>? ExceptionThrown
	{
		add => _locationService.ExceptionThrown += value;
		remove => _locationService.ExceptionThrown -= value;
	}

	public void ResetLocationInfo() => _locationService.CurrentService?.ResetLocationInfo();

	public void ForceSetLocationInfo(int stationIndex, bool isRunningToNextStation)
		=> _locationService.ForceSetLocationInfo(stationIndex, isRunningToNextStation);

	public void SetTimetableRows(TimetableRow[]? rows)
	{
		StaLocationInfo[]? locations = rows
			?.Where(static v => !v.IsInfoRow)
			.Select(static v => new StaLocationInfo(
				v.Location.Location_m,
				v.Location.Longitude_deg,
				v.Location.Latitude_deg,
				v.Location.OnStationDetectRadius_m))
			.ToArray();
		_locationService.SetStationLocations(locations);
	}
}
