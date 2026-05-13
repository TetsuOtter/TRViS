namespace TRViS.Services;

/// <summary>
/// MAUI GPS API（Geolocation）から LocationService への橋渡しアダプター。
/// GPS関連のMAUI依存コードをすべてここに集約する。
/// </summary>
internal class LocationServiceGpsAdapter : IDisposable
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private static readonly NLog.Logger locationServiceLogger = LoggerService.GetLocationServiceLogger();

	private readonly LocationService _locationService;
	private static Permissions.LocationWhenInUse LocationWhenInUsePermission { get; } = new();

	public event EventHandler<Location?>? OnGpsLocationUpdated;

	private const GeolocationAccuracy GEOLOCATION_ACCURACY = GeolocationAccuracy.High;
	bool isGpsListeningFeatureSupported = true;

	public LocationServiceGpsAdapter(LocationService locationService)
	{
		_locationService = locationService;

		// Subscribe to IsEnabled changes to start/stop GPS listening
		_locationService.IsEnabledChanged += OnLocationServiceIsEnabledChanged;
	}

	private void OnLocationServiceIsEnabledChanged(object? sender, TRViS.Utils.ValueChangedEventArgs<bool> e)
	{
		if (e.NewValue)
		{
			// NetworkSyncService が現在の location source の場合は端末 GPS を起動しない。
			// 位置情報はサーバーから配信されるため、端末 GPS は電力の無駄になる。
			if (_locationService.CurrentService is not LonLatLocationService)
			{
				logger.Info("LocationService.IsEnabled changed to true but CurrentService is not LonLatLocationService -> skip GPS listening");
				return;
			}
			logger.Info("LocationService.IsEnabled changed to true -> StartGpsListening");
			_ = StartGpsListeningAsync();
		}
		else
		{
			logger.Info("LocationService.IsEnabled changed to false -> StopGpsListening");
			StopGpsListening();
		}
	}

	private CancellationTokenSource? _gpsCts;

	private async Task StartGpsListeningAsync()
	{
		_gpsCts?.Cancel();
		_gpsCts?.Dispose();
		CancellationTokenSource cts = new();
		_gpsCts = cts;

		await GpsPositioningTask(cts.Token);
	}

	private void StopGpsListening()
	{
		_gpsCts?.Cancel();
	}

	private async Task GpsPositioningTask(CancellationToken token)
	{
		if (!await RequestPermissionAsync())
		{
			return;
		}

		bool isFirst = true;
		void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
		{
			OnGpsLocationGot(e.Location, ref isFirst, false);
		}
		void OnLocationListeningFailed(object? sender, GeolocationListeningFailedEventArgs e)
		{
			logger.Error("Location Service Listening Failed: {0}", e.Error);
			locationServiceLogger.Error("Location Service Listening Failed: {0}", e.Error);
			switch (e.Error)
			{
				case GeolocationError.Unauthorized:
					logger.Error("Location Service Permission Denied");
					_locationService.OnGpsListeningFailed(new Exception("Location Service Permission Denied"));
					return;
				case GeolocationError.PositionUnavailable:
					logger.Error("Location Service Location Unavailable");
					Task.Run(async () =>
					{
						try
						{
							bool success = await Geolocation.Default.StartListeningForegroundAsync(new(GEOLOCATION_ACCURACY, _locationService.Interval));
							logger.Info("Location Service StartListeningForegroundAsync: {0}", success);
						}
						catch (PlatformNotSupportedException ex)
						{
							logger.Error(ex, "Location Service StartListeningForegroundAsync failed");
							isGpsListeningFeatureSupported = false;
							_locationService.OnGpsListeningFailed(ex);
							return;
						}
						catch (Exception ex)
						{
							logger.Error(ex, "Location Service StartListeningForegroundAsync failed");
							_locationService.OnGpsListeningFailed(ex);
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
				isListenStarted = Geolocation.Default.IsListeningForeground || await Geolocation.Default.StartListeningForegroundAsync(new(GEOLOCATION_ACCURACY, _locationService.Interval));
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
			_locationService.OnGpsListeningFailed(ex);
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
			await GpsPositioningTaskWithIntervalAsync(token);
			logger.Info("Location Service Ended");
		}
	}

	private async Task GpsPositioningTaskWithIntervalAsync(CancellationToken token)
	{
		GeolocationRequest req = new(GEOLOCATION_ACCURACY, _locationService.Interval);
		logger.Info("Starting Location Service... (Interval: {0})", _locationService.Interval);
		locationServiceLogger.Info("Starting Location Service... (Interval: {0})", _locationService.Interval);

		bool isFirst = true;
		while (!token.IsCancellationRequested)
		{
			logger.Trace("Location Service Loop");
			DateTime executeStartTime = DateTime.Now;
			TimeSpan timeout = _locationService.Interval;
			req.Timeout = timeout;

			if (!await RequestPermissionAsync())
			{
				return;
			}

			try
			{
				Location? loc = await Geolocation.Default.GetLocationAsync(req, token);
				OnGpsLocationGot(loc, ref isFirst, true);
			}
			catch (Exception ex)
			{
				logger.Error(ex, "GetLocationAsync failed");
				locationServiceLogger.Error(ex, "GetLocationAsync failed");
				_locationService.OnGpsListeningFailed(ex);
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

	private async Task<bool> RequestPermissionAsync()
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
			_locationService.OnGpsListeningFailed(ex);
			return false;
		}
		switch (permissionStatus)
		{
			case PermissionStatus.Disabled:
			case PermissionStatus.Denied:
			case PermissionStatus.Unknown:
				logger.Error("Location Service Permission Disabled, Denied or Unknown state");
				locationServiceLogger.Error("Location Service Permission Disabled, Denied or Unknown state");
				_locationService.OnGpsListeningFailed(new Exception("Location Service Permission Disabled, Denied or Unknown state"));
				return false;
			case PermissionStatus.Granted:
				logger.Trace("Location Service Permission Granted");
				return true;
		}
		logger.Error("Location Service Permission Unknown state: {0}", permissionStatus);
		return false;
	}

	private void OnGpsLocationGot(Location? loc, ref bool isFirst, bool useAverageDistance)
	{
		if (loc is null)
		{
			locationServiceLogger.Warn("Location Service Positioning Failed");
			OnGpsLocationUpdated?.Invoke(this, null);
			return;
		}

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
		OnGpsLocationUpdated?.Invoke(this, loc);
		_locationService.SetGpsLocation(loc.Longitude, loc.Latitude, loc.Accuracy, useAverageDistance);
	}

	private bool _disposed;
	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;
		_locationService.IsEnabledChanged -= OnLocationServiceIsEnabledChanged;
		StopGpsListening();
		_gpsCts?.Dispose();
	}
}
