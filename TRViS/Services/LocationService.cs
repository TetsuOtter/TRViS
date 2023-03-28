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
	[ObservableProperty]
	bool _IsEnabled;

	[ObservableProperty]
	TimeSpan _Interval;

	[ObservableProperty]
	Location? _NearbyCenter;

	[ObservableProperty]
	double _NearbyRadius_m = 300;

	bool _IsNearby;
	public bool IsNearby
	{
		get => _IsNearby;
		private set
		{
			if (_IsNearby == value)
				return;

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
			if (value == _LastLocation
				|| _LastLocation is null
				|| value?.Equals(_LastLocation) != false
			)
				return;

			this.OnPropertyChanging(nameof(LastLocation));

			double? distance = value?.CalculateDistance(NearbyCenter, DistanceUnits.Kilometers) * 1000;
			IsNearby = (distance is double v && v <= NearbyRadius_m);

			Location? lastLocation = _LastLocation;
			_LastLocation = value;

			this.OnPropertyChanged(nameof(LastLocation));
			LastLocationChanged?.Invoke(this, lastLocation, value);
		}
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
			gpsCancelation?.Cancel();
		else
			Task.Run(StartGPS);
	}

	partial void OnNearbyCenterChanged(Location? newValue)
	{
		if (LastLocation is null || newValue is null)
			return;

		double? distance = LastLocation.CalculateDistance(newValue, DistanceUnits.Kilometers) * 1000;
		IsNearby = (distance is double v && v <= NearbyRadius_m);
	}

	public Task StartGPS()
	{
		gpsCancelation?.Cancel();
		gpsCancelation?.Dispose();
		gpsCancelation = null;

		if (!IsEnabled)
			return Task.CompletedTask;

		gpsCancelation = new CancellationTokenSource();
		CancellationToken token = gpsCancelation.Token;

		return Task.Run(async () =>
		{
			// ref: https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation
			// accuracy: 30m - 500m
			GeolocationRequest req = new(GeolocationAccuracy.Default, Interval);

			while (!token.IsCancellationRequested)
			{
				TimeSpan timeout = Interval;
				req.Timeout = timeout;

				await Task.WhenAll(new Task[]
				{
					CheckAndNotifyCurrentLocation(req, token),
					Task.Delay(timeout, token),
				});
			}
		}, token);
	}

	async Task CheckAndNotifyCurrentLocation(GeolocationRequest req, CancellationToken token)
	{
		try
		{
			Location? loc = await Geolocation.Default.GetLocationAsync(req, token);

			if (loc is not null)
				LastLocation = loc;
		}
		catch (Exception ex)
		{
			IsEnabled = false;
			gpsCancelation?.Cancel();
			System.Diagnostics.Debug.WriteLine(ex);

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
