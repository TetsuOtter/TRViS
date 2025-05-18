using System.Threading.Tasks;

namespace TRViS.Services;

public partial class LocationService
{
	static Permissions.LocationWhenInUse LocationWhenInUsePermission { get; } = new();
	public event EventHandler<Location?>? OnGpsLocationUpdated;

	private const GeolocationAccuracy GEOLOCATION_ACCURACY = GeolocationAccuracy.High;
	bool isGpsListeningFeatureSupported = true;
	async Task GpsPositioningTask(ILocationService service, CancellationToken token)
	{
		if (service is not LonLatLocationService gpsService)
		{
			logger.Error("GpsPositioningTask is called with non-LonLatLocationService");
			IsEnabled = false;
			serviceCancellation?.Cancel();
			ExceptionThrown?.Invoke(this, new Exception("GpsPositioningTask is called with non-LonLatLocationService"));
			return;
		}

		if (!await RequestPermissionAsync())
		{
			return;
		}

		bool isFirst = true;
		void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
		{
			OnLocationInfoGot(gpsService, e.Location, ref isFirst, false);
		}
		void OnLocationListeningFailed(object? sender, GeolocationListeningFailedEventArgs e)
		{
			logger.Error("Location Service Listening Failed: {0}", e.Error);
			locationServiceLogger.Error("Location Service Listening Failed: {0}", e.Error);
			switch (e.Error)
			{
				case GeolocationError.Unauthorized:
					logger.Error("Location Service Permission Denied");
					locationServiceLogger.Error("Location Service Permission Denied");
					IsEnabled = false;
					serviceCancellation?.Cancel();
					ExceptionThrown?.Invoke(this, new Exception("Location Service Permission Denied"));
					return;
				case GeolocationError.PositionUnavailable:
					logger.Error("Location Service Location Unavailable");
					locationServiceLogger.Error("Location Service Location Unavailable");
					Task.Run(async () =>
					{
						try
						{
							bool success = await Geolocation.Default.StartListeningForegroundAsync(new(GEOLOCATION_ACCURACY, Interval));
							logger.Info("Location Service StartListeningForegroundAsync: {0}", success);
							locationServiceLogger.Info("Location Service StartListeningForegroundAsync: {0}", success);
						}
						catch (PlatformNotSupportedException ex)
						{
							logger.Error(ex, "Location Service StartListeningForegroundAsync failed");
							locationServiceLogger.Error(ex, "Location Service StartListeningForegroundAsync failed");
							isGpsListeningFeatureSupported = false;
							IsEnabled = false;
							serviceCancellation?.Cancel();
							ExceptionThrown?.Invoke(this, ex);
							return;
						}
						catch (Exception ex)
						{
							logger.Error(ex, "Location Service StartListeningForegroundAsync failed");
							locationServiceLogger.Error(ex, "Location Service StartListeningForegroundAsync failed");
							IsEnabled = false;
							serviceCancellation?.Cancel();
							ExceptionThrown?.Invoke(this, ex);
							return;
						}
					}, token);
					break;
			}
		}
		bool isListenStarted = false;
		try
		{
			Geolocation.Default.LocationChanged += OnLocationChanged;
			Geolocation.Default.ListeningFailed += OnLocationListeningFailed;
			if (isGpsListeningFeatureSupported)
			{
				isListenStarted = Geolocation.Default.IsListeningForeground || await Geolocation.Default.StartListeningForegroundAsync(new(GEOLOCATION_ACCURACY, Interval));
				locationServiceLogger.Info("Location Service StartListeningForegroundAsync: {0}", isListenStarted);
			}
			else
			{
				locationServiceLogger.Warn("Location Service StartListeningForegroundAsync is not supported");
			}
		}
		catch (FeatureNotSupportedException fnse)
		{
			logger.Info(fnse, "FeatureNotSupportedException");
			isGpsListeningFeatureSupported = false;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "StartListeningForegroundAsync failed");
			locationServiceLogger.Error(ex, "StartListeningForegroundAsync failed");
			IsEnabled = false;
			serviceCancellation?.Cancel();

			if (ExceptionThrown is null)
				throw;
			else
				ExceptionThrown.Invoke(this, ex);
			return;
		}
		finally
		{
			if (!isListenStarted)
			{
				Geolocation.Default.LocationChanged -= OnLocationChanged;
				Geolocation.Default.ListeningFailed -= OnLocationListeningFailed;
			}
		}

		if (isListenStarted)
		{
			logger.Info("Starting Location Service...");
			locationServiceLogger.Info("Starting Location Service...");
			token.Register(() =>
			{
				logger.Info("Location Service Cancelation Requested");
				locationServiceLogger.Info("Location Service Cancelation Requested");
				if (!Geolocation.Default.IsListeningForeground)
				{
					return;
				}
				Geolocation.Default.StopListeningForeground();
				Geolocation.Default.LocationChanged -= OnLocationChanged;
				Geolocation.Default.ListeningFailed -= OnLocationListeningFailed;
			});
		}
		else
		{
			await GpsPositioningTaskWithIntervalAsync(gpsService, token);
			logger.Info("Location Service Ended");
		}
	}

	async Task GpsPositioningTaskWithIntervalAsync(LonLatLocationService service, CancellationToken token)
	{
		// ref: https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation
		// accuracy: 30m - 500m
		GeolocationRequest req = new(GEOLOCATION_ACCURACY, Interval);
		logger.Info("Starting Location Service... (Interval: {0})", Interval);
		locationServiceLogger.Info("Starting Location Service... (Interval: {0})", Interval);

		bool isFirst = true;
		while (!token.IsCancellationRequested)
		{
			logger.Trace("Location Service Loop");
			DateTime executeStartTime = DateTime.Now;
			TimeSpan timeout = Interval;
			req.Timeout = timeout;

			if (!await RequestPermissionAsync())
			{
				return;
			}
			Location? loc = null;

			try
			{
				loc = await Geolocation.Default.GetLocationAsync(req, token);
				OnLocationInfoGot(service, loc, ref isFirst, true);
			}
			catch (Exception ex)
			{
				logger.Error(ex, "GetLocationAsync failed");
				locationServiceLogger.Error(ex, "GetLocationAsync failed");
				IsEnabled = false;
				serviceCancellation?.Cancel();

				if (ExceptionThrown is null)
					throw;
				else
					ExceptionThrown.Invoke(this, ex);
				return;
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
				locationServiceLogger.Info("Location Service Positioning Took {0}", executeEndTime - executeStartTime);
				await Task.Delay(timeout - (executeEndTime - executeStartTime), token);
			}
			else
			{
				logger.Warn("Location Service Positioning Took Too Long (time: {0})", executeEndTime - executeStartTime);
			}
		}
	}

	async Task<bool> RequestPermissionAsync()
	{
		PermissionStatus permissionStatus = await LocationWhenInUsePermission.CheckStatusAsync();
		logger.Trace("Location Service Current Permission Status: {0}", permissionStatus);
		if (permissionStatus == PermissionStatus.Granted)
		{
			return true;
		}

		try
		{
			permissionStatus = await MainThread.InvokeOnMainThreadAsync(LocationWhenInUsePermission.RequestAsync);
			logger.Trace("Location Service Requested Permission Status: {0}", permissionStatus);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Location Service Request Permission Failed");
			IsEnabled = false;
			serviceCancellation?.Cancel();

			if (ExceptionThrown is null)
				throw;
			else
				ExceptionThrown.Invoke(this, ex);
			return false;
		}
		switch (permissionStatus)
		{
			case PermissionStatus.Disabled:
			case PermissionStatus.Denied:
			case PermissionStatus.Unknown:
				logger.Error("Location Service Permission Disabled, Denied or Unknown state");
				locationServiceLogger.Error("Location Service Permission Disabled, Denied or Unknown state");
				IsEnabled = false;
				serviceCancellation?.Cancel();
				ExceptionThrown?.Invoke(this, new Exception("Location Service Permission Disabled, Denied or Unknown state"));
				return false;
			case PermissionStatus.Granted:
				logger.Trace("Location Service Permission Granted");
				return true;
		}
		logger.Error("Location Service Permission Unknown state: {0}", permissionStatus);
		return false;
	}

	void OnLocationInfoGot(LonLatLocationService service, Location? loc, ref bool isFirst, bool useAverageDistance)
	{
		if (loc is null)
		{
			locationServiceLogger.Warn("Location Service Positioning Failed");
		}
		else
		{
			locationServiceLogger.Info(
				"Location Service Positioning Success (lon: {0}, lat: {1}, alt:{2}({3}), accuracy: {4}(alt: {5}), time: {6}, course: {7})",
				loc.Longitude,
				loc.Latitude,
				loc.Altitude,
				loc.AltitudeReferenceSystem,
				loc.Accuracy,
				loc.VerticalAccuracy,
				loc.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
				loc.Course
			);
		}
		OnGpsLocationUpdated?.Invoke(this, loc);
		if (loc is null)
		{
			return;
		}

		if (isFirst)
		{
			logger.Info("Location Service First Positioning");
			isFirst = false;
			service.ForceSetLocationInfo(loc.Longitude, loc.Latitude);
		}
		else
		{
			double distance = service.SetCurrentLocation(loc.Longitude, loc.Latitude, useAverageDistance);
			if (double.IsNaN(distance))
			{
				IsEnabled = false;
			}
		}
	}
}
