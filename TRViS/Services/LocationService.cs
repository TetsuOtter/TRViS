using System.ComponentModel;

using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Utils;
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
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private static readonly NLog.Logger locationServiceLogger = LoggerService.GetLocationServiceLogger();
	private static readonly NLog.Logger lonLatLocationServiceLogger = LoggerService.GetLocationServiceLoggerT<LonLatLocationService>();

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

	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;

	public event EventHandler<int>? TimeChanged;
	public event EventHandler<Exception>? ExceptionThrown;
	public event EventHandler<TimetableData>? TimetableUpdated;

	public bool CanUseService => _CurrentService?.CanUseService ?? false;

	public ILocationService? CurrentService => _CurrentService;

	/// <summary>
	/// NetworkSyncService の CanStart 状態。NetworkSyncService が使用されていない場合は false
	/// </summary>
	public bool NetworkSyncServiceCanStart => (_CurrentService as NetworkSyncServiceBase)?.CanStart ?? false;

	ILocationService? _CurrentService;
	CancellationTokenSource? serviceCancellation = null;
	CancellationTokenSource? timeProviderCancellation = null;

	public LocationService()
	{
		logger.Trace("Creating...");

		IsEnabled = false;
		SetLonLatLocationService();

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
		if (targetService is NetworkSyncServiceBase)
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
	void OnTimetableUpdated(object? sender, TimetableData timetableData)
	{
		logger.Debug("TimetableUpdated: WorkGroupId={0}, WorkId={1}, TrainId={2}, Scope={3}",
			timetableData.WorkGroupId, timetableData.WorkId, timetableData.TrainId, timetableData.Scope);
		MainThread.BeginInvokeOnMainThread(() => TimetableUpdated?.Invoke(sender, timetableData));
	}

	void OnNetworkSyncServiceCanStartChanged(object? sender, bool canStart)
	{
		logger.Debug("NetworkSyncServiceCanStartChanged: {0}", canStart);

		// WebSocket接続時にのみ、CanStartがtrueになったら自動で「運行開始」と「位置情報ON」をする
		if (canStart && _CurrentService is WebSocketNetworkSyncService)
		{
			logger.Info("CanStart is true and WebSocket is being used -> automatically enable location service");
			MainThread.BeginInvokeOnMainThread(() =>
			{
				IsEnabled = true;
			});
		}
	}

	void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_CurrentService is NetworkSyncServiceBase networkSyncService)
		{
			switch (e.PropertyName)
			{
				case nameof(AppViewModel.SelectedWorkGroup):
					networkSyncService.WorkGroupId = InstanceManager.AppViewModel.SelectedWorkGroup?.Id;
					break;
				case nameof(AppViewModel.SelectedWork):
					networkSyncService.WorkId = InstanceManager.AppViewModel.SelectedWork?.Id;
					break;
				case nameof(AppViewModel.SelectedTrainData):
					networkSyncService.TrainId = InstanceManager.AppViewModel.SelectedTrainData?.Id;
					break;
			}
		}
	}

	void OnNetworkSyncServiceConnectionClosed(object? sender, EventArgs e)
	{
		logger.Info("NetworkSyncService connection closed -> switching to LonLatLocationService");
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			await Util.DisplayAlert(
				"接続切断",
				"ネットワークサービスとの接続が切断されました。GPS測位モードに切り替えます。",
				"OK"
			);
			SetLonLatLocationService();
		});
	}

	void OnNetworkSyncServiceConnectionFailed(object? sender, EventArgs e)
	{
		logger.Warn("NetworkSyncService connection failed after reconnection attempts -> showing dialog");
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			await Util.DisplayAlert(
				"接続失敗",
				"ネットワークサービスへの接続に失敗しました。GPS測位モードに切り替えます。",
				"OK"
			);
			logger.Info("NetworkSyncService connection failed -> switching to LonLatLocationService");
			SetLonLatLocationService();
		});
	}

	public void SetLonLatLocationService()
	{
		logger.Trace("Setting LonLatLocationService...");
		locationServiceLogger.Info("Setting LonLatLocationService...");

		if (IsEnabled)
		{
			logger.Debug("IsEnabled is true -> stop Current LocationService");
			IsEnabled = false;
		}

		ILocationService? currentService = _CurrentService;
		LonLatLocationService nextService = new(lonLatLocationServiceLogger);
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
		if (currentService is NetworkSyncServiceBase networkSyncService)
		{
			networkSyncService.TimeChanged -= OnTimeChanged;
			networkSyncService.TimetableUpdated -= OnTimetableUpdated;
		}
		if (currentService is IDisposable disposable)
			disposable.Dispose();

		CancellationTokenSource nextTokenSource = new();
		timeProviderCancellation = nextTokenSource;
		// バックグラウンドで実行し続ける
		_ = Task.Run(async () =>
		{
			int lastTime_s = -1;
			while (!nextTokenSource.Token.IsCancellationRequested)
			{
				// logger.Trace("TimeProviderTask Loop");

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

					if (ExceptionThrown is null)
						throw;
					else
						ExceptionThrown.Invoke(this, ex);
					return;
				}
			}
		});
	}

	bool isIdChangedEventHandlerSet = false;
	public async Task SetNetworkSyncServiceAsync(Uri uri, CancellationToken? token = null)
	{
		logger.Trace("Setting NetworkSyncService...");

		if (IsEnabled)
		{
			logger.Debug("IsEnabled is true -> stop Current LocationService");
			IsEnabled = false;
		}

		NetworkSyncServiceBase nextService = await NetworkSyncServiceUtil.CreateFromUriAsync(uri, InstanceManager.HttpClient, token);

		await ChangeNetworkSyncServiceAsync(nextService);
	}

	public Task SetNetworkSyncServiceAsync(NetworkSyncServiceBase nextService)
	{
		logger.Trace("Setting NetworkSyncService...");

		if (IsEnabled)
		{
			logger.Debug("IsEnabled is true -> stop Current LocationService");
			IsEnabled = false;
		}

		return ChangeNetworkSyncServiceAsync(nextService);
	}

	private async Task ChangeNetworkSyncServiceAsync(NetworkSyncServiceBase nextService)
	{
		logger.Trace("Changing NetworkSyncService...");

		ILocationService? currentService = _CurrentService;
		if (currentService is not null)
		{
			logger.Debug("CurrentService is not null -> remove EventHandlers");
			currentService.CanUseServiceChanged -= OnCanUseServiceChanged;
			currentService.LocationStateChanged -= OnLocationStateChanged;
		}

		nextService.CanUseServiceChanged += OnCanUseServiceChanged;
		nextService.LocationStateChanged += OnLocationStateChanged;
		nextService.TimeChanged += OnTimeChanged;
		nextService.TimetableUpdated += OnTimetableUpdated;
		nextService.ConnectionClosed += OnNetworkSyncServiceConnectionClosed;
		nextService.ConnectionFailed += OnNetworkSyncServiceConnectionFailed;
		nextService.CanStartChanged += OnNetworkSyncServiceCanStartChanged;
		nextService.StaLocationInfo = currentService?.StaLocationInfo;
		nextService.WorkGroupId = InstanceManager.AppViewModel.SelectedWorkGroup?.Id;
		nextService.WorkId = InstanceManager.AppViewModel.SelectedWork?.Id;
		nextService.TrainId = InstanceManager.AppViewModel.SelectedTrainData?.Id;
		if (!isIdChangedEventHandlerSet)
		{
			logger.Debug("Add EventHandlers for AppViewModel.PropertyChanged");
			InstanceManager.AppViewModel.PropertyChanged += OnAppViewModelPropertyChanged;
			isIdChangedEventHandlerSet = true;
		}
		_CurrentService = nextService;
		if (nextService.CanUseService != currentService?.CanUseService)
			await MainThread.InvokeOnMainThreadAsync(() => CanUseServiceChanged?.Invoke(this, nextService.CanUseService));

		timeProviderCancellation?.Cancel();
		timeProviderCancellation?.Dispose();
		timeProviderCancellation = null;
		serviceCancellation?.Cancel();
		serviceCancellation?.Dispose();
		serviceCancellation = null;
		if (currentService is NetworkSyncServiceBase networkSyncService)
		{
			networkSyncService.TimeChanged -= OnTimeChanged;
			networkSyncService.TimetableUpdated -= OnTimetableUpdated;
			networkSyncService.ConnectionClosed -= OnNetworkSyncServiceConnectionClosed;
			networkSyncService.ConnectionFailed -= OnNetworkSyncServiceConnectionFailed;
			networkSyncService.CanStartChanged -= OnNetworkSyncServiceCanStartChanged;
		}
		if (currentService is IDisposable disposable)
			disposable.Dispose();

		if (nextService is not WebSocketNetworkSyncService)
		{
			CancellationTokenSource nextTokenSource = new();
			serviceCancellation = nextTokenSource;
			// バックグラウンドで実行し続ける
			_ = Task.Run(() => NetworkSyncServiceTask(nextService, nextTokenSource.Token));
		}
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

		_CurrentService.StaLocationInfo = timetableRows?.Where(static v => !v.IsInfoRow).Select(static v => new StaLocationInfo(v.Location.Location_m, v.Location.Longitude_deg, v.Location.Latitude_deg, v.Location.OnStationDetectRadius_m)).ToArray();
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
				if (isIdChangedEventHandlerSet)
				{
					InstanceManager.AppViewModel.PropertyChanged -= OnAppViewModelPropertyChanged;
					isIdChangedEventHandlerSet = false;
				}
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
