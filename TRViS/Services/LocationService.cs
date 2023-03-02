using DependencyPropertyGenerator;

namespace TRViS.Services;

public class ExceptionThrownEventArgs : EventArgs
{
	public Exception Exception { get; }

	public ExceptionThrownEventArgs(Exception ex)
	{
		this.Exception = ex;
	}
}

[DependencyProperty<bool>("IsEnabled")]
[DependencyProperty<TimeSpan>("Interval")]
[DependencyProperty<Location>("NearbyCenter")]
[DependencyProperty<double>("NearbyRadius_m", DefaultValue = 300)]
[DependencyProperty<bool>("IsNearby", IsReadOnly = true)]
[DependencyProperty<Location>("LastLocation", IsReadOnly = true)]
public partial class LocationService : IDisposable
{
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

	partial void OnIsNearbyChanged(bool oldValue, bool newValue)
	{
		IsNearbyChanged?.Invoke(this, oldValue, newValue);
	}

	partial void OnLastLocationChanged(Location? oldValue, Location? newValue)
	{
		double? distance = newValue?.CalculateDistance(NearbyCenter, DistanceUnits.Kilometers) * 1000;
		IsNearby = (distance is double v && v <= NearbyRadius_m);
		LastLocationChanged?.Invoke(this, oldValue, newValue);
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
			while (!token.IsCancellationRequested)
			{
				// ref: https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation
				// accuracy: 30m - 500m
				GeolocationRequest req = new(GeolocationAccuracy.Default, Interval);

				Application.Current?.Dispatcher
					.DispatchAsync(() => CheckAndNotifyCurrentLocation(req, token))
					.ConfigureAwait(false);

				await Task.Delay(Interval, token).ConfigureAwait(false);
			}
		}, token);
	}

	async void CheckAndNotifyCurrentLocation(GeolocationRequest req, CancellationToken token)
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
