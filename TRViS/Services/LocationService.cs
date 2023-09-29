using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Controls;

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

	[ObservableProperty]
	TimeSpan _Interval;

	[ObservableProperty]
	Location? _NearbyCenter;

	[ObservableProperty]
	double _NearbyRadius_m = DefaultNearbyRadius_m;
	public const double DefaultNearbyRadius_m = 200;

	bool _IsNearby;
	public bool IsNearby
	{
		get => _IsNearby;
		private set
		{
			if (_IsNearby == value)
				return;

			logger.Debug("IsNearby is changed to {0}", value);
			this.OnPropertyChanging(nameof(IsNearby));
			_IsNearby = value;
			this.OnPropertyChanged(nameof(IsNearby));
			IsNearbyChanged?.Invoke(this, !value, value);
		}
	}

	Location? _LastLocation;
	public Location? LastLocation
	{
		get => _LastLocation;

		private set
		{
			if (
				value == _LastLocation
				|| (_LastLocation is not null && value?.Equals(_LastLocation) != false)
			)
			{
				logger.Trace("LastLocation is already {0}, so skipping...", value);
				return;
			}

			logger.Info("LastLocation is changing to {0}", value);
			this.OnPropertyChanging(nameof(LastLocation));

			setIsNearby(value);

			Location? lastLocation = _LastLocation;
			_LastLocation = value;

			this.OnPropertyChanged(nameof(LastLocation));
			LastLocationChanged?.Invoke(this, lastLocation, value);
		}
	}

	void setIsNearby(in Location? location)
	{
		if (NearbyCenter is null || location is null)
		{
			logger.Trace("NearbyCenter is null or location is null -> do nothing");
			return;
		}

		double distance = location.CalculateDistance(NearbyCenter, DistanceUnits.Kilometers) * 1000;

		bool isNearby = distance <= NearbyRadius_m;
		logger.Info("IsNearby: {0} (= distance: {1} <= NearbyRadius_m: {2})", isNearby, distance, NearbyRadius_m);
		logger.Debug("Station Lon:{4:F5}, Lat:{5:F5}\tCurrent Lon:{0:F5}, Lat:{1:F5}",
			NearbyCenter.Longitude,
			NearbyCenter.Latitude,
			location.Longitude,
			location.Latitude
		);

		LogView.Add(
			LogView.Priority.Info,

			$"setIsNearby() Lon:{location.Longitude:F5}, Lat:{location.Latitude:F5}"
			+ $" (Distance:{distance:F2}m/{NearbyRadius_m:F2}m from Lon:{NearbyCenter.Longitude:F5}, Lat:{NearbyCenter.Latitude:F5} -> IsNearBy:{isNearby})"
		);
		IsNearby = isNearby;
	}

	public event EventHandler<Exception>? ExceptionThrown;
	public event ValueChangedEventHandler<bool>? IsNearbyChanged;
	public event ValueChangedEventHandler<Location?>? LastLocationChanged;

	CancellationTokenSource? gpsCancelation;

	private bool disposedValue;

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

	partial void OnNearbyCenterChanged(Location? newValue)
	{
		if (LastLocation is null || newValue is null)
		{
			logger.Trace("LastLocation is null or newValue is null -> do nothing");
			return;
		}

		logger.Info("NearbyCenter is changed to {0}", newValue);
		IsNearby = false;
		setIsNearby(LastLocation);
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
		CancellationToken token = gpsCancelation.Token;
		LastLocation = null;
		_IsNearby = true;

		return Task.Run(async () =>
		{
			// ref: https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation
			// accuracy: 30m - 500m
			GeolocationRequest req = new(GeolocationAccuracy.Default, Interval);
			logger.Info("Starting Location Service... (Interval: {0})", Interval);

			LogView.Add("Location Service Starting...");
			while (!token.IsCancellationRequested)
			{
				logger.Trace("Location Service Loop");
				TimeSpan timeout = Interval;
				req.Timeout = timeout;

				await Task.WhenAll(new Task[]
				{
					CheckAndNotifyCurrentLocation(req, token),
					Task.Delay(timeout, token),
				});
			}

			logger.Info("Location Service Ended");
			LogView.Add("Location Service Ended");
		}, token);
	}

	async Task CheckAndNotifyCurrentLocation(GeolocationRequest req, CancellationToken token)
	{
		logger.Trace("Starting...");

		try
		{
			Location? loc = await MainThread.InvokeOnMainThreadAsync(() => Geolocation.Default.GetLocationAsync(req, token));

			if (loc is not null)
			{
				logger.Debug("CurrentLocation is {0}", loc);
				LastLocation = loc;
			}
			else
			{
				logger.Warn("CurrentLocation is null");
				LogView.Add("CurrentLocation is UNKNOWN (value was null)");
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "GetLocationAsync failed");
			IsEnabled = false;
			gpsCancelation?.Cancel();
			System.Diagnostics.Debug.WriteLine(ex);
			LogView.Add(LogView.Priority.Error, "GetLocationAsync failed:" + ex.ToString());

			if (ExceptionThrown is null)
				throw;
			else
				ExceptionThrown.Invoke(this, ex);
		}
	}

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
