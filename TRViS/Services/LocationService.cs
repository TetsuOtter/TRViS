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
				return;

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
			return;

		double distance = location.CalculateDistance(NearbyCenter, DistanceUnits.Kilometers) * 1000;

		IsNearby = distance <= NearbyRadius_m;
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

		IsNearby = false;
		setIsNearby(LastLocation);
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
		LastLocation = null;
		_IsNearby = true;

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
			Location? loc = await MainThread.InvokeOnMainThreadAsync(() => Geolocation.Default.GetLocationAsync(req, token));

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
