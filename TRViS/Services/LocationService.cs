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

public partial class LocationService : IDisposable
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	public bool IsEnabled
	{
		get => _CurrentService?.IsEnabled ?? false;
		set
		{
			if (_CurrentService is null || _CurrentService.IsEnabled == value)
				return;

			_CurrentService.IsEnabled = value;
			OnIsEnabledChanged(value);
			IsEnabledChanged?.Invoke(this, new ValueChangedEventArgs<bool>(!value, value));
		}
	}
	public event EventHandler<ValueChangedEventArgs<bool>>? IsEnabledChanged;

	public event EventHandler<bool>? CanUseServiceChanged;

	public event EventHandler<LocationStateChangedEventArgs> LocationStateChanged;

	public event EventHandler<int>? TimeChanged;
	public event EventHandler<Exception>? ExceptionThrown;

	public bool CanUseService => _CurrentService?.CanUseService ?? false;

	ILocationService? _CurrentService;
	CancellationTokenSource? serviceCancellation = null;
	CancellationTokenSource? timeProviderCancellation = null;

	public LocationService()
	{
		logger.Trace("Creating...");

		IsEnabled = false;
		SetLonLatLocationService();

		LocationStateChanged += (sender, e) =>
		{
			StaLocationInfo? newStaLocationInfo = _CurrentService?.StaLocationInfo?.ElementAtOrDefault(e.NewStationIndex);
			LogView.Add($"LocationStateChanged: Station[{e.NewStationIndex}]@({newStaLocationInfo?.Location_lon_deg}, {newStaLocationInfo?.Location_lat_deg} & Radius:{newStaLocationInfo?.NearbyRadius_m}) IsRunningToNextStation:{e.IsRunningToNextStation}");
		};

		logger.Debug("LocationService is created");
	}

	void OnIsEnabledChanged(bool value)
	{
		if (_CurrentService is null)
		{
			logger.Debug("_CurrentService is null -> do nothing");
			return;
		}

		ILocationService targetService = _CurrentService;
		if (targetService is NetworkSyncService)
		{
			logger.Debug("NetworkSyncService is used -> do nothing");
		}
		else if (targetService is LonLatLocationService)
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
				Task.Run(() => GpsPositioningTask(targetService, nextTokenSource.Token));
			}
		}
	}

	void OnCanUseServiceChanged(object? sender, bool e)
	{
		logger.Debug("CanUseService is changed to {0}", e);
		MainThread.BeginInvokeOnMainThread(() => CanUseServiceChanged?.Invoke(sender, e));
	}
	void OnLocationStateChanged(object? sender, LocationStateChangedEventArgs e)
	{
		logger.Debug("LocationStateChanged: Station[{0}] IsRunningToNextStation:{1}", e.NewStationIndex, e.IsRunningToNextStation);
		MainThread.BeginInvokeOnMainThread(() => LocationStateChanged?.Invoke(sender, e));
	}
	void OnTimeChanged(object? sender, int second)
	{
		logger.Debug("TimeChanged: {0}", second);
		MainThread.BeginInvokeOnMainThread(() => TimeChanged?.Invoke(sender, second));
	}

	public void SetLonLatLocationService()
	{
		logger.Trace("Setting LonLatLocationService...");

		if (IsEnabled)
		{
			logger.Debug("IsEnabled is true -> stop Current LocationService");
			IsEnabled = false;
		}

		ILocationService? currentService = _CurrentService;
		LonLatLocationService nextService = new();
		if (currentService is not null)
		{
			currentService.CanUseServiceChanged -= OnCanUseServiceChanged;
			currentService.LocationStateChanged -= OnLocationStateChanged;
		}

		nextService.CanUseServiceChanged += OnCanUseServiceChanged;
		nextService.LocationStateChanged += OnLocationStateChanged;
		nextService.StaLocationInfo = currentService?.StaLocationInfo;
		_CurrentService = nextService;
		if (nextService.CanUseService != currentService?.CanUseService)
			CanUseServiceChanged?.Invoke(this, nextService.CanUseService);

		timeProviderCancellation?.Cancel();
		timeProviderCancellation?.Dispose();
		timeProviderCancellation = null;
		serviceCancellation?.Cancel();
		serviceCancellation?.Dispose();
		serviceCancellation = null;
		if (currentService is NetworkSyncService networkSyncService)
			networkSyncService.TimeChanged -= OnTimeChanged;
		if (currentService is IDisposable disposable)
			disposable.Dispose();

		CancellationTokenSource nextTokenSource = new();
		timeProviderCancellation = nextTokenSource;
		// バックグラウンドで実行し続ける
		_ = Task.Run(async () => {
			int lastTime_s = -1;
			while (!nextTokenSource.Token.IsCancellationRequested)
			{
				logger.Trace("TimeProviderTask Loop");

				if (nextTokenSource.Token.IsCancellationRequested)
				{
					logger.Debug("Cancellation is requested -> break");
					break;
				}

				try
				{
					int currentTime_s = (int)DateTime.Now.TimeOfDay.TotalSeconds;
					if (lastTime_s != currentTime_s)
					{
						lastTime_s = currentTime_s;
						OnTimeChanged(this, currentTime_s);
					}
					// 複雑なロジックは面倒なので、単純に100ms待つ
					await Task.Delay(100, nextTokenSource.Token);
				}
				catch (TaskCanceledException)
				{
					logger.Debug("Task is canceled -> break");
					break;
				}
				catch (Exception ex)
				{
					logger.Error(ex, "TimeProviderTask Loop Failed");
					IsEnabled = false;
					timeProviderCancellation?.Cancel();
					LogView.Add(LogView.Priority.Error, "TimeProviderTask Loop Failed:" + ex.ToString());

					if (ExceptionThrown is null)
						throw;
					else
						ExceptionThrown.Invoke(this, ex);
					return;
				}
			}
		});
	}

	public async Task SetNetworkSyncServiceAsync(Uri uri, CancellationToken? token = null)
	{
		logger.Trace("Setting NetworkSyncService...");

		if (IsEnabled)
		{
			logger.Debug("IsEnabled is true -> stop Current LocationService");
			IsEnabled = false;
		}

		ILocationService? currentService = _CurrentService;
		NetworkSyncService nextService = await NetworkSyncService.CreateFromUriAsync(uri, InstanceManager.HttpClient, token);
		if (currentService is not null)
		{
			currentService.CanUseServiceChanged -= OnCanUseServiceChanged;
			currentService.LocationStateChanged -= OnLocationStateChanged;
		}

		nextService.CanUseServiceChanged += OnCanUseServiceChanged;
		nextService.LocationStateChanged += OnLocationStateChanged;
		nextService.TimeChanged += OnTimeChanged;
		nextService.StaLocationInfo = currentService?.StaLocationInfo;
		_CurrentService = nextService;
		if (nextService.CanUseService != currentService?.CanUseService)
			CanUseServiceChanged?.Invoke(this, nextService.CanUseService);

		timeProviderCancellation?.Cancel();
		timeProviderCancellation?.Dispose();
		timeProviderCancellation = null;
		serviceCancellation?.Cancel();
		serviceCancellation?.Dispose();
		serviceCancellation = null;
		if (currentService is NetworkSyncService networkSyncService)
			networkSyncService.TimeChanged -= OnTimeChanged;
		if (currentService is IDisposable disposable)
			disposable.Dispose();

		CancellationTokenSource nextTokenSource = new();
		serviceCancellation = nextTokenSource;
		// バックグラウンドで実行し続ける
		_ = Task.Run(() => NetworkSyncServiceTask(nextService, nextTokenSource.Token));
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
