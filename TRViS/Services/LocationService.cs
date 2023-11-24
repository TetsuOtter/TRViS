using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Controls;
using TRViS.IO.Models;
using TRViS.MyAppCustomizables;
using TRViS.ViewModels;

namespace TRViS.Services;

public class ExceptionThrownEventArgs : EventArgs
{
	public Exception Exception { get; }

	public ExceptionThrownEventArgs(Exception ex)
	{
		this.Exception = ex;
	}
}

public partial class LocationService : ObservableObject, IDisposable
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	[ObservableProperty]
	bool _IsEnabled;

	readonly LonLatLocationService LonLatLocationService;

	public event EventHandler<LocationStateChangedEventArgs> LocationStateChanged
	{
		add => LonLatLocationService.LocationStateChanged += value;
		remove => LonLatLocationService.LocationStateChanged -= value;
	}

	public const double DefaultNearbyRadius_m = 200;

	public event EventHandler<Exception>? ExceptionThrown;

	CancellationTokenSource? gpsCancelation;

	public LocationService()
	{
		logger.Trace("Creating...");

		IsEnabled = false;
		LonLatLocationService = new();

		LocationStateChanged += (sender, e) =>
		{
			LogView.Add($"LocationStateChanged: Station[{e.NewStationIndex}]@({LonLatLocationService.StaLocationInfo?[e.NewStationIndex].Location_lon_deg}, {LonLatLocationService.StaLocationInfo?[e.NewStationIndex].Location_lat_deg} & Radius:{LonLatLocationService.StaLocationInfo?[e.NewStationIndex].NearbyRadius_m}) IsRunningToNextStation:{e.IsRunningToNextStation}");
		};

		logger.Debug("LocationService is created");
	}

	partial void OnIsEnabledChanged(bool newValue)
	{
		// GPS停止
		if (!newValue)
		{
			logger.Info("IsEnabled is changed to false -> stop GPS");
			gpsCancelation?.Cancel();
		}
		else
		{
			logger.Info("IsEnabled is changed to true -> start GPS");
			Task.Run(StartGPS);
		}
	}

	public void SetTimetableRows(TimetableRow[]? timetableRows)
	{
		logger.Trace("Setting TimetableRows...");

		IsEnabled = false;
		LonLatLocationService.StaLocationInfo = timetableRows?.Select(v => new StaLocationInfo(v.Location.Location_m, v.Location.Longitude_deg, v.Location.Latitude_deg, v.Location.OnStationDetectRadius_m)).ToArray();
	}

	static EasterEggPageViewModel EasterEggPageViewModel { get; } = InstanceManager.EasterEggPageViewModel;
	static double lastInterval = 1;
	static TimeSpan lastIntervalTimeSpan = TimeSpan.FromSeconds(lastInterval);
	static TimeSpan Interval
	{
		get
		{
			if (lastInterval == EasterEggPageViewModel.LocationServiceInterval_Seconds)
				return lastIntervalTimeSpan;

			logger.Info("Interval is changed to {0}[s]", EasterEggPageViewModel.LocationServiceInterval_Seconds);
			lastInterval = EasterEggPageViewModel.LocationServiceInterval_Seconds;
			lastIntervalTimeSpan = TimeSpan.FromSeconds(lastInterval);
			return lastIntervalTimeSpan;
		}
	}
	public Task StartGPS()
	{
		logger.Trace("Starting...");

		gpsCancelation?.Cancel();
		gpsCancelation?.Dispose();
		gpsCancelation = null;

		if (!IsEnabled)
		{
			logger.Debug("IsEnabled is false -> do nothing");
			return Task.CompletedTask;
		}

		gpsCancelation = new CancellationTokenSource();

		return Task.Run(PositioningTask, gpsCancelation.Token);
	}

	static Permissions.LocationWhenInUse LocationWhenInUsePermission { get; } = new();
	async Task PositioningTask()
	{
		// ref: https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation
		// accuracy: 30m - 500m
		GeolocationRequest req = new(GeolocationAccuracy.Default, Interval);
		logger.Info("Starting Location Service... (Interval: {0})", Interval);

		if (gpsCancelation?.Token is not CancellationToken token)
		{
			logger.Warn("gpsCancelation is null -> do nothing");
			return;
		}

		LogView.Add("Location Service Starting...");
		bool isFirst = true;
		while (!token.IsCancellationRequested)
		{
			logger.Trace("Location Service Loop");
			DateTime executeStartTime = DateTime.Now;
			TimeSpan timeout = Interval;
			req.Timeout = timeout;

			PermissionStatus permissionStatus = await LocationWhenInUsePermission.CheckStatusAsync();
			logger.Trace("Location Service Current Permission Status: {0}", permissionStatus);
			if (permissionStatus != PermissionStatus.Granted)
			{
				try
				{
					
					permissionStatus = await MainThread.InvokeOnMainThreadAsync(LocationWhenInUsePermission.RequestAsync);
					logger.Trace("Location Service Requested Permission Status: {0}", permissionStatus);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Location Service Request Permission Failed");
					IsEnabled = false;
					gpsCancelation?.Cancel();
					LogView.Add(LogView.Priority.Error, "Location Service Request Permission Failed:" + ex.ToString());

					if (ExceptionThrown is null)
						throw;
					else
						ExceptionThrown.Invoke(this, ex);
					return;
				}
			}
			switch (permissionStatus)
			{
				case PermissionStatus.Disabled:
				case PermissionStatus.Denied:
				case PermissionStatus.Unknown:
					logger.Error("Location Service Permission Disabled, Denied or Unknown state");
					IsEnabled = false;
					gpsCancelation?.Cancel();
					ExceptionThrown?.Invoke(this, new Exception("Location Service Permission Disabled, Denied or Unknown state"));
					return;
			}
			logger.Trace("Location Service Permission Granted");
			Location? loc = null;

			try
			{
				loc = await Geolocation.Default.GetLocationAsync(req, token);
			}
			catch (Exception ex)
			{
				logger.Error(ex, "GetLocationAsync failed");
				IsEnabled = false;
				gpsCancelation?.Cancel();
				LogView.Add(LogView.Priority.Error, "GetLocationAsync failed:" + ex.ToString());

				if (ExceptionThrown is null)
					throw;
				else
					ExceptionThrown.Invoke(this, ex);
				return;
			}

			if (loc is not null)
			{
				if (isFirst)
				{
					logger.Info("Location Service First Positioning");
					LogView.Add($"Location Service Started with lonlat: ({loc.Longitude}, {loc.Latitude})");
					isFirst = false;
					LonLatLocationService.ForceSetLocationInfo(loc.Longitude, loc.Latitude);
				}
				else
				{
					LonLatLocationService.SetCurrentLocation(loc.Longitude, loc.Latitude);
					LogView.Add($"lonlat: ({loc.Longitude}, {loc.Latitude})");
				}
				logger.Trace("Location Service Positioning Success (lon: {0}, lat: {1})", loc.Longitude, loc.Latitude);
			}
			else
			{
				logger.Warn("Location Service Positioning Failed");
				LogView.Add("Location Service Positioning Failed");
			}

			if (token.IsCancellationRequested)
			{
				logger.Debug("gpsCancelation is requested -> break");
				break;
			}
			DateTime executeEndTime = DateTime.Now;
			if (executeEndTime < (executeStartTime + timeout))
			{
				logger.Trace("Location Service Positioning Took {0}", executeEndTime - executeStartTime);
				await Task.Delay(timeout - (executeEndTime - executeStartTime), token);
			}
			else
			{
				logger.Warn("Location Service Positioning Took Too Long (time: {0})", executeEndTime - executeStartTime);
			}
		}

		logger.Info("Location Service Ended");
		LogView.Add("Location Service Ended");
	}
	private bool disposedValue;

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			gpsCancelation?.Cancel();

			if (disposing)
			{
				gpsCancelation?.Dispose();
				gpsCancelation = null;
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
