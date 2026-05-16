using TRViS.NetworkSyncService;
using TRViS.Utils;
using TRViS.LocationService.Abstractions;

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
	private readonly NLog.Logger logger;
	private readonly NLog.Logger locationServiceLogger;
	private readonly NLog.Logger lonLatLocationServiceLogger;
	private readonly HttpClient httpClient;
	private readonly ITimeProvider timeProvider;

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
	public event EventHandler<ServerInfo>? ServerInfoUpdated;
	public event EventHandler<DiagramInfo>? DiagramInfoUpdated;
	public event EventHandler<SelectTrainCommand>? TrainSelectionRequested;
	public event EventHandler<OperationCommand>? OperationCommandReceived;
	public event EventHandler<HeaderColorCommand>? HeaderColorChangeRequested;
	public event EventHandler<NotificationData>? NotificationReceived;
	public event EventHandler<TimeFormatCommand>? TimeFormatChangeRequested;

	/// <summary>
	/// GPS位置情報が更新された際に発生するイベント。
	/// 引数は (longitude, latitude, accuracy?) の tuple。
	/// </summary>
	public event EventHandler<(double Longitude, double Latitude, double? Accuracy)>? OnGpsLocationUpdated;

	/// <summary>
	/// ユーザーへのアラート要求イベント（MAUI非依存）。メインアプリ側で購読してUIを表示する。
	/// </summary>
	public event EventHandler<UserAlertRequestedEventArgs>? AlertRequested;

	public bool CanUseService => _CurrentService?.CanUseService ?? false;

	public ILocationService? CurrentService => _CurrentService;

	/// <summary>
	/// NetworkSyncService の CanStart 状態。NetworkSyncService が使用されていない場合は false
	/// </summary>
	public bool NetworkSyncServiceCanStart => (_CurrentService as NetworkSyncServiceBase)?.CanStart ?? false;

	/// <summary>
	/// GPS位置情報の更新間隔
	/// </summary>
	public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

	ILocationService? _CurrentService;
	CancellationTokenSource? serviceCancellation = null;
	CancellationTokenSource? timeProviderCancellation = null;

	// GPS isFirst リセット追跡
	bool _isFirstGps = true;

	// SetTargetIds キャッシュ
	string? _lastWorkGroupId;
	string? _lastWorkId;
	string? _lastTrainId;

	public LocationService(NLog.Logger logger, NLog.Logger locationServiceLogger, NLog.Logger lonLatLocationServiceLogger, HttpClient httpClient, ITimeProvider timeProvider)
	{
		this.logger = logger;
		this.locationServiceLogger = locationServiceLogger;
		this.lonLatLocationServiceLogger = lonLatLocationServiceLogger;
		this.httpClient = httpClient;
		this.timeProvider = timeProvider;

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
				_isFirstGps = true;
				serviceCancellation?.Cancel();
				serviceCancellation?.Dispose();
				logger.Info("IsEnabled is changed to true -> start LocationService (GPS will be driven externally)");
				CancellationTokenSource nextTokenSource = new();
				serviceCancellation = nextTokenSource;
			}
		}
	}

	void OnCanUseServiceChanged(object? sender, bool e)
	{
		logger.Debug("CanUseService is changed to {0}", e);
		CanUseServiceChanged?.Invoke(sender, e);
	}
	void OnLocationStateChanged(object? sender, LocationStateChangedEventArgs e)
	{
		logger.Debug("LocationStateChanged: Station[{0}] IsRunningToNextStation:{1}", e.NewStationIndex, e.IsRunningToNextStation);
		LocationStateChanged?.Invoke(sender, e);
	}
	void OnTimeChanged(object? sender, int second)
	{
		TimeChanged?.Invoke(sender, second);
	}
	void OnTimetableUpdated(object? sender, TimetableData timetableData)
	{
		logger.Debug("TimetableUpdated: WorkGroupId={0}, WorkId={1}, TrainId={2}, Scope={3}",
			timetableData.WorkGroupId, timetableData.WorkId, timetableData.TrainId, timetableData.Scope);
		TimetableUpdated?.Invoke(sender, timetableData);
	}

	void OnServerInfoUpdated(object? sender, ServerInfo info) => ServerInfoUpdated?.Invoke(sender, info);
	void OnDiagramInfoUpdated(object? sender, DiagramInfo info) => DiagramInfoUpdated?.Invoke(sender, info);
	void OnTrainSelectionRequested(object? sender, SelectTrainCommand cmd) => TrainSelectionRequested?.Invoke(sender, cmd);

	/// <summary>
	/// 運行操作コマンドを受信したときの処理。
	/// 位置情報サービスの有効/無効はここで適用し、運行開始/終了は AppViewModel 側に委譲する。
	/// </summary>
	void OnOperationCommandReceived(object? sender, OperationCommand cmd)
	{
		logger.Info("OperationCommandReceived: Action={0}", cmd.Action);
		switch (cmd.Action)
		{
			case OperationCommandType.EnableLocationService:
			case OperationCommandType.StartOperation:
				// StartOperation は位置情報サービスを ON にすることで運行を開始する
				IsEnabled = true;
				break;
			case OperationCommandType.DisableLocationService:
			case OperationCommandType.EndOperation:
				IsEnabled = false;
				break;
		}
		OperationCommandReceived?.Invoke(sender, cmd);
	}

	void OnHeaderColorChangeRequested(object? sender, HeaderColorCommand cmd) => HeaderColorChangeRequested?.Invoke(sender, cmd);
	void OnNotificationReceived(object? sender, NotificationData n) => NotificationReceived?.Invoke(sender, n);
	void OnTimeFormatChangeRequested(object? sender, TimeFormatCommand cmd) => TimeFormatChangeRequested?.Invoke(sender, cmd);

	/// <summary>
	/// NetworkSyncService 経由で配信された緯度経度を受け取り、
	/// GPS 由来と同じ <see cref="OnGpsLocationUpdated"/> イベントとして外部に通知する。
	/// 端末 GPS の代わりにサーバー配信を使う構成のために、station 検出は <see cref="SyncedData.Location_m"/>
	/// 側に任せ、ここでは Map / UI 表示用のイベント発火だけ行う。
	/// </summary>
	void OnNetworkSyncServiceLonLatLocationReceived(object? sender, (double Longitude, double Latitude, double? Accuracy) location)
	{
		locationServiceLogger.Info(
			"NetworkSyncService LonLatLocation Received (lon: {0}, lat: {1}, accuracy: {2})",
			location.Longitude, location.Latitude, location.Accuracy
		);
		OnGpsLocationUpdated?.Invoke(this, location);
	}

	void OnNetworkSyncServiceCanStartChanged(object? sender, bool canStart)
	{
		logger.Debug("NetworkSyncServiceCanStartChanged: {0}", canStart);

		// WebSocket接続時にのみ、CanStartがtrueになったら自動で「運行開始」と「位置情報ON」をする
		if (canStart && _CurrentService is WebSocketNetworkSyncService)
		{
			logger.Info("CanStart is true and WebSocket is being used -> automatically enable location service");
			IsEnabled = true;
		}
	}

	void OnNetworkSyncServiceConnectionClosed(object? sender, EventArgs e)
	{
		logger.Info("NetworkSyncService connection closed -> switching to LonLatLocationService");
		AlertRequested?.Invoke(this, new UserAlertRequestedEventArgs(
			"接続切断",
			"ネットワークサービスとの接続が切断されました。GPS測位モードに切り替えます。",
			"OK"
		));
		SetLonLatLocationService();
	}

	void OnNetworkSyncServiceConnectionFailed(object? sender, EventArgs e)
	{
		logger.Warn("NetworkSyncService connection failed after reconnection attempts -> showing dialog");
		AlertRequested?.Invoke(this, new UserAlertRequestedEventArgs(
			"接続失敗",
			"ネットワークサービスへの接続に失敗しました。GPS測位モードに切り替えます。",
			"OK"
		));
		logger.Info("NetworkSyncService connection failed -> switching to LonLatLocationService");
		SetLonLatLocationService();
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
		_isFirstGps = true;

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
			networkSyncService.ServerInfoUpdated -= OnServerInfoUpdated;
			networkSyncService.DiagramInfoUpdated -= OnDiagramInfoUpdated;
			networkSyncService.TrainSelectionRequested -= OnTrainSelectionRequested;
			networkSyncService.OperationCommandReceived -= OnOperationCommandReceived;
			networkSyncService.HeaderColorChangeRequested -= OnHeaderColorChangeRequested;
			networkSyncService.NotificationReceived -= OnNotificationReceived;
			networkSyncService.TimeFormatChangeRequested -= OnTimeFormatChangeRequested;
			networkSyncService.LonLatLocationReceived -= OnNetworkSyncServiceLonLatLocationReceived;
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
				if (nextTokenSource.Token.IsCancellationRequested)
				{
					logger.Debug("Cancellation is requested -> break");
					break;
				}

				try
				{
					int currentTime_s = timeProvider.GetCurrentTimeSeconds();
					if (lastTime_s != currentTime_s)
					{
						lastTime_s = currentTime_s;
						OnTimeChanged(this, currentTime_s);
					}
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

	/// <summary>
	/// 駅位置情報を設定する。TimetableRow → StaLocationInfo 変換はLogic層で行うこと。
	/// </summary>
	public void SetStationLocations(StaLocationInfo[]? locations)
	{
		logger.Trace("Setting StationLocations...");

		IsEnabled = false;
		if (_CurrentService is null)
		{
			logger.Debug("_CurrentService is null -> do nothing");
			return;
		}

		_CurrentService.StaLocationInfo = locations;
	}

	public async Task SetNetworkSyncServiceAsync(Uri uri, CancellationToken? token = null)
	{
		logger.Trace("Setting NetworkSyncService...");

		if (IsEnabled)
		{
			logger.Debug("IsEnabled is true -> stop Current LocationService");
			IsEnabled = false;
		}

		NetworkSyncServiceBase nextService = await NetworkSyncServiceUtil.CreateFromUriAsync(uri, httpClient, token);

		ChangeNetworkSyncService(nextService);
	}

	public void SetNetworkSyncService(NetworkSyncServiceBase nextService)
	{
		logger.Trace("Setting NetworkSyncService...");

		if (IsEnabled)
		{
			logger.Debug("IsEnabled is true -> stop Current LocationService");
			IsEnabled = false;
		}

		ChangeNetworkSyncService(nextService);
	}

	private void ChangeNetworkSyncService(NetworkSyncServiceBase nextService)
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
		nextService.ServerInfoUpdated += OnServerInfoUpdated;
		nextService.DiagramInfoUpdated += OnDiagramInfoUpdated;
		nextService.TrainSelectionRequested += OnTrainSelectionRequested;
		nextService.OperationCommandReceived += OnOperationCommandReceived;
		nextService.HeaderColorChangeRequested += OnHeaderColorChangeRequested;
		nextService.NotificationReceived += OnNotificationReceived;
		nextService.TimeFormatChangeRequested += OnTimeFormatChangeRequested;
		nextService.ConnectionClosed += OnNetworkSyncServiceConnectionClosed;
		nextService.ConnectionFailed += OnNetworkSyncServiceConnectionFailed;
		nextService.CanStartChanged += OnNetworkSyncServiceCanStartChanged;
		nextService.LonLatLocationReceived += OnNetworkSyncServiceLonLatLocationReceived;
		nextService.StaLocationInfo = currentService?.StaLocationInfo;
		// キャッシュされた ID を設定
		nextService.WorkGroupId = _lastWorkGroupId;
		nextService.WorkId = _lastWorkId;
		nextService.TrainId = _lastTrainId;

		_CurrentService = nextService;
		if (nextService.CanUseService != currentService?.CanUseService)
		{
			CanUseServiceChanged?.Invoke(this, nextService.CanUseService);
		}

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
			networkSyncService.ServerInfoUpdated -= OnServerInfoUpdated;
			networkSyncService.DiagramInfoUpdated -= OnDiagramInfoUpdated;
			networkSyncService.TrainSelectionRequested -= OnTrainSelectionRequested;
			networkSyncService.OperationCommandReceived -= OnOperationCommandReceived;
			networkSyncService.HeaderColorChangeRequested -= OnHeaderColorChangeRequested;
			networkSyncService.NotificationReceived -= OnNotificationReceived;
			networkSyncService.TimeFormatChangeRequested -= OnTimeFormatChangeRequested;
			networkSyncService.ConnectionClosed -= OnNetworkSyncServiceConnectionClosed;
			networkSyncService.ConnectionFailed -= OnNetworkSyncServiceConnectionFailed;
			networkSyncService.CanStartChanged -= OnNetworkSyncServiceCanStartChanged;
			networkSyncService.LonLatLocationReceived -= OnNetworkSyncServiceLonLatLocationReceived;
		}
		if (currentService is IDisposable disposable)
			disposable.Dispose();

		// CanStartChanged は値の遷移時にしか発火しない (エッジトリガ)。WebSocket は
		// SetNetworkSyncService より前に ConnectAsync 済みで、最初の SyncedData
		// (CanStart false->true) をここで購読する前に受信し得る。その場合 false->true
		// の遷移が失われ、以後 SyncedData が来ても CanStart は true のまま遷移しないため
		// 自動 IsEnabled=true が二度と走らない (特にサーバが接続直後に状態を push する
		// 再接続時に位置情報が disable のまま固定される)。購読完了後に現在の CanStart
		// レベルを取り込んで補正する。OnNetworkSyncServiceCanStartChanged 側で
		// WebSocket 限定ガードが掛かっているため HTTP の挙動は変わらない。
		if (nextService.CanStart)
			OnNetworkSyncServiceCanStartChanged(nextService, true);

		if (nextService is not WebSocketNetworkSyncService)
		{
			CancellationTokenSource nextTokenSource = new();
			serviceCancellation = nextTokenSource;
			// バックグラウンドで実行し続ける
			_ = Task.Run(() => NetworkSyncServiceTask(nextService, nextTokenSource.Token));
		}
	}

	/// <summary>
	/// 対象 WorkGroupId / WorkId / TrainId を設定する。
	/// NetworkSyncService が現在使用中の場合は即座に反映する。
	/// </summary>
	public void SetTargetIds(string? workGroupId, string? workId, string? trainId)
	{
		logger.Debug("SetTargetIds: workGroupId={0}, workId={1}, trainId={2}", workGroupId, workId, trainId);
		_lastWorkGroupId = workGroupId;
		_lastWorkId = workId;
		_lastTrainId = trainId;

		if (_CurrentService is NetworkSyncServiceBase networkSyncService)
		{
			networkSyncService.WorkGroupId = workGroupId;
			networkSyncService.WorkId = workId;
			networkSyncService.TrainId = trainId;
		}
	}

	/// <summary>
	/// GPS位置情報を外部（メインアプリ側）から push する。
	/// isFirst が true のとき ForceSetLocationInfo を呼ぶ。
	/// </summary>
	/// <param name="longitude">経度</param>
	/// <param name="latitude">緯度</param>
	/// <param name="accuracy">精度（任意）</param>
	/// <param name="useAverageDistance">移動距離の平均を使用するか（listen=false, polling=true）</param>
	public void SetGpsLocation(double longitude, double latitude, double? accuracy, bool useAverageDistance = true)
	{
		locationServiceLogger.Info(
			"SetGpsLocation (lon: {0}, lat: {1}, accuracy: {2}, useAverageDistance: {3})",
			longitude, latitude, accuracy, useAverageDistance
		);
		OnGpsLocationUpdated?.Invoke(this, (longitude, latitude, accuracy));

		if (!IsEnabled)
		{
			locationServiceLogger.Debug("IsEnabled is false -> skip GPS location update");
			return;
		}

		if (_CurrentService is not LonLatLocationService gpsService)
		{
			locationServiceLogger.Debug("CurrentService is not LonLatLocationService -> skip GPS location update");
			return;
		}

		if (_isFirstGps)
		{
			logger.Info("SetGpsLocation: First call -> ForceSetLocationInfo");
			_isFirstGps = false;
			gpsService.ForceSetLocationInfo(longitude, latitude);
		}
		else
		{
			double distance = gpsService.SetCurrentLocation(longitude, latitude, useAverageDistance);
			if (double.IsNaN(distance))
			{
				IsEnabled = false;
			}
		}
	}

	/// <summary>
	/// GPS リスニング失敗を通知する（メインアプリ側の GPS adapter から呼ぶ）
	/// </summary>
	public void OnGpsListeningFailed(Exception ex)
	{
		logger.Error(ex, "GPS Listening Failed");
		locationServiceLogger.Error("GPS Listening Failed: {0}", ex.Message);
		IsEnabled = false;
		serviceCancellation?.Cancel();
		ExceptionThrown?.Invoke(this, ex);
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
			timeProviderCancellation?.Cancel();

			if (disposing)
			{
				serviceCancellation?.Dispose();
				serviceCancellation = null;
				timeProviderCancellation?.Dispose();
				timeProviderCancellation = null;
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
