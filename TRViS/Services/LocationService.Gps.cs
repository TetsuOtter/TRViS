namespace TRViS.Services;

public partial class LocationService
{
	static Permissions.LocationWhenInUse LocationWhenInUsePermission { get; } = new();
	public event EventHandler<Location?>? OnGpsLocationUpdated;
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

		// ref: https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation
		// accuracy: 30m - 500m
		GeolocationRequest req = new(GeolocationAccuracy.Default, Interval);
		logger.Info("Starting Location Service... (Interval: {0})", Interval);
		locationServiceLogger.Info("Starting Location Service... (Interval: {0})", Interval);

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
					serviceCancellation?.Cancel();

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
					locationServiceLogger.Error("Location Service Permission Disabled, Denied or Unknown state");
					IsEnabled = false;
					serviceCancellation?.Cancel();
					ExceptionThrown?.Invoke(this, new Exception("Location Service Permission Disabled, Denied or Unknown state"));
					return;
			}
			logger.Trace("Location Service Permission Granted");
			Location? loc = null;

			try
			{
				loc = await Geolocation.Default.GetLocationAsync(req, token);
				locationServiceLogger.Info("Location Service Positioning Success (lon: {0}, lat: {1})", loc?.Longitude, loc?.Latitude);
				OnGpsLocationUpdated?.Invoke(this, loc);
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

			if (loc is not null)
			{
				if (isFirst)
				{
					logger.Info("Location Service First Positioning");
					isFirst = false;
					gpsService.ForceSetLocationInfo(loc.Longitude, loc.Latitude);
				}
				else
				{
					double distance = gpsService.SetCurrentLocation(loc.Longitude, loc.Latitude);
					if (double.IsNaN(distance))
					{
						IsEnabled = false;
					}
				}
				logger.Trace("Location Service Positioning Success (lon: {0}, lat: {1})", loc.Longitude, loc.Latitude);
			}
			else
			{
				logger.Warn("Location Service Positioning Failed");
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

		logger.Info("Location Service Ended");
	}
}
