using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Controls;
using TRViS.IO.Models;
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
	public event EventHandler<ValueChangedEventArgs<bool>>? IsEnabledChanged;

	public event EventHandler<bool>? CanUseServiceChanged;

	public event EventHandler<LocationStateChangedEventArgs> LocationStateChanged;

	public event EventHandler<Exception>? ExceptionThrown;

	public bool CanUseService => _CurrentService.CanUseService;

	ILocationService _CurrentService;
	Func<ILocationService, CancellationToken, Task> _ServiceTask;
	CancellationTokenSource? serviceCancellation;

	public LocationService()
	{
		logger.Trace("Creating...");

		_CurrentService = new LonLatLocationService();
		_ServiceTask = GpsPositioningTask;
		IsEnabled = false;

		LocationStateChanged += (sender, e) =>
		{
			StaLocationInfo? newStaLocationInfo = _CurrentService?.StaLocationInfo?.ElementAtOrDefault(e.NewStationIndex);
			LogView.Add($"LocationStateChanged: Station[{e.NewStationIndex}]@({newStaLocationInfo?.Location_lon_deg}, {newStaLocationInfo?.Location_lat_deg} & Radius:{newStaLocationInfo?.NearbyRadius_m}) IsRunningToNextStation:{e.IsRunningToNextStation}");
		};

		logger.Debug("LocationService is created");
	}

	partial void OnIsEnabledChanged(bool value)
	{
		// GPS停止
		if (!value)
		{
			logger.Info("IsEnabled is changed to false -> stop LocationService");
			serviceCancellation?.Cancel();
		}
		else
		{
			serviceCancellation?.Cancel();
			serviceCancellation?.Dispose();
			logger.Info("IsEnabled is changed to true -> start LocationService");
			CancellationTokenSource nextTokenSource = new();
			serviceCancellation = nextTokenSource;

			EventHandler<bool> CanUseServiceChangedEventHandler = (object? sender, bool e) => MainThread.BeginInvokeOnMainThread(() => CanUseServiceChanged?.Invoke(sender, e));
			EventHandler<LocationStateChangedEventArgs> LocationStateChangedEventHandler = (object? sender, LocationStateChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(() => LocationStateChanged?.Invoke(sender, e));

			ILocationService targetService = _CurrentService;
			Func<ILocationService, CancellationToken, Task> serviceTask = _ServiceTask;

			targetService.CanUseServiceChanged += CanUseServiceChangedEventHandler;
			targetService.LocationStateChanged += LocationStateChangedEventHandler;
			Task.Run(() => serviceTask(targetService, nextTokenSource.Token).ContinueWith((_) => {
				targetService.CanUseServiceChanged -= CanUseServiceChangedEventHandler;
				targetService.LocationStateChanged -= LocationStateChangedEventHandler;
			}), nextTokenSource.Token);
		}
		IsEnabledChanged?.Invoke(this, new ValueChangedEventArgs<bool>(!value, value));
	}

	public async Task SetNetworkSyncServiceAsync(Uri uri, CancellationToken? token = null)
	{
		logger.Trace("Setting NetworkSyncService...");

		if (IsEnabled)
		{
			logger.Debug("IsEnabled is true -> stop LocationService");
			IsEnabled = false;
		}

		NetworkSyncService networkSyncService = await NetworkSyncService.CreateFromUriAsync(uri, InstanceManager.HttpClient, token);
		networkSyncService.StaLocationInfo = _CurrentService.StaLocationInfo;
		ILocationService service = _CurrentService;
		_CurrentService = networkSyncService;
		_ServiceTask = NetworkSyncServiceTask;
		if (networkSyncService.CanUseService != service.CanUseService)
			CanUseServiceChanged?.Invoke(this, networkSyncService.CanUseService);
		if (service is IDisposable disposable)
			disposable.Dispose();
	}

	public void SetTimetableRows(TimetableRow[]? timetableRows)
	{
		logger.Trace("Setting TimetableRows...");

		IsEnabled = false;
		if (_CurrentService is null)
		{
			logger.Debug("_CurrentService is null -> do nothing");
			return;
		}

		_CurrentService.StaLocationInfo = timetableRows?.Where(v => !v.IsInfoRow).Select(v => new StaLocationInfo(v.Location.Location_m, v.Location.Longitude_deg, v.Location.Latitude_deg, v.Location.OnStationDetectRadius_m)).ToArray();
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

	public void ForceSetLocationInfo(int row, bool isRunningToNextStation)
	{
		if (!IsEnabled)
		{
			logger.Debug("IsEnabled is false -> do nothing");
			return;
		}

		logger.Debug("ForceSetLocationInfo({0}, {1})", row, isRunningToNextStation);
		_CurrentService?.ForceSetLocationInfo(row, isRunningToNextStation);
		logger.Debug("Done");
	}

	private bool disposedValue;

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			serviceCancellation?.Cancel();

			if (disposing)
			{
				serviceCancellation?.Dispose();
				serviceCancellation = null;
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
