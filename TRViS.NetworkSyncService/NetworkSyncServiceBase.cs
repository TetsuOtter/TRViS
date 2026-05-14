using System;
using System.Threading;
using System.Threading.Tasks;

using NLog;

using TRViS.LocationService.Abstractions;
using TRViS.NetworkSyncService.Internals;

namespace TRViS.NetworkSyncService;

/// <summary>
/// Base class for NetworkSyncService manager that handles common functionality
/// for both HTTP and WebSocket implementations
/// </summary>
public abstract class NetworkSyncServiceBase : ILocationService, IDisposable
{
	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	public bool IsEnabled { get; set; }
	private bool _CanUseService = false;
	public bool CanUseService
	{
		get => _CanUseService;
		protected set
		{
			if (_CanUseService == value)
				return;

			_CanUseService = value;
			CanUseServiceChanged?.Invoke(this, value);
		}
	}

	private bool _CanStart = false;
	public bool CanStart
	{
		get => _CanStart;
		protected set
		{
			if (_CanStart == value)
				return;

			_CanStart = value;
			CanStartChanged?.Invoke(this, value);
		}
	}

	private StaLocationInfo[]? _staLocationInfo;
	public StaLocationInfo[]? StaLocationInfo
	{
		get => _staLocationInfo;
		set
		{
			if (_staLocationInfo == value)
				return;

			// 旧 current 駅を控えておく (新配列で対応駅を Location_m で探すため)。
			StaLocationInfo? oldCurrentStation = null;
			if (_staLocationInfo is not null
				&& CurrentStationIndex >= 0
				&& CurrentStationIndex < _staLocationInfo.Length)
			{
				oldCurrentStation = _staLocationInfo[CurrentStationIndex];
			}
			int oldIndex = CurrentStationIndex;
			bool oldRunning = IsRunningToNextStation;

			_staLocationInfo = value;
			_lonLatStationDetector.SetStationLocations(value);

			// #245: 「現在の位置情報サービスの状態をもとに、更新後の駅 index を計算する」
			// 旧 current 駅が新配列にも存在するならその index に追従させ、走行フラグを引き継ぐ。
			// (= 「前の駅が削除された」「後の駅が追加された」等の編集で index がずれたケース)
			// 存在しなければ駅自体が削除されたものとみなしてリセットする。
			int newIndex = FindStationIndexByLocation_m(value, oldCurrentStation);
			if (newIndex < 0)
			{
				ResetLocationInfo();
				return;
			}
			if (newIndex == oldIndex && oldRunning == IsRunningToNextStation)
			{
				// 状態が変わらない: _lonLatStationDetector は上の SetStationLocations で履歴クリア
				// 済みなので Sync で同期だけ取り、外部にはイベントを飛ばさない。
				_lonLatStationDetector.Sync(newIndex, oldRunning);
				return;
			}
			ForceSetLocationInfo(newIndex, oldRunning);
		}
	}

	private static int FindStationIndexByLocation_m(StaLocationInfo[]? newArray, StaLocationInfo? oldStation)
	{
		if (newArray is null || oldStation is null)
			return -1;
		double key = oldStation.Location_m;
		if (double.IsNaN(key))
			return -1;
		for (int i = 0; i < newArray.Length; i++)
		{
			if (newArray[i].Location_m == key)
				return i;
		}
		return -1;
	}

	// Location_m が NaN かつ緯度経度が配信されたときの駅判定エンジン。
	// SyncedData が <see cref="SyncedData.Location_m"/> を持たない接続向けのフォールバック。
	private readonly LonLatStationDetector _lonLatStationDetector = new();

	private string? _WorkGroupId;
	public string? WorkGroupId
	{
		get => _WorkGroupId;
		set
		{
			if (_WorkGroupId == value)
				return;
			_WorkGroupId = value;
			OnWorkGroupIdChanged(value);
		}
	}

	private string? _WorkId;
	public string? WorkId
	{
		get => _WorkId;
		set
		{
			if (_WorkId == value)
				return;
			_WorkId = value;
			OnWorkIdChanged(value);
		}
	}

	private string? _TrainId;
	public string? TrainId
	{
		get => _TrainId;
		set
		{
			if (_TrainId == value)
				return;
			_TrainId = value;
			OnTrainIdChanged(value);
		}
	}

	public int CurrentStationIndex { get; private set; }

	public bool IsRunningToNextStation { get; private set; }

	public bool IsTimeServiceEnabled { get; private set; }
	public int CurrentTime_s { get; private set; }

	public event EventHandler<bool>? CanUseServiceChanged;
	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;
	public event EventHandler<int>? TimeChanged;
	public event EventHandler<TimetableData>? TimetableUpdated;
	public event EventHandler<ServerInfo>? ServerInfoUpdated;
	public event EventHandler<DiagramInfo>? DiagramInfoUpdated;
	public event EventHandler<SelectTrainCommand>? TrainSelectionRequested;
	public event EventHandler<OperationCommand>? OperationCommandReceived;
	public event EventHandler<HeaderColorCommand>? HeaderColorChangeRequested;
	public event EventHandler<NotificationData>? NotificationReceived;
	public event EventHandler<TimeFormatCommand>? TimeFormatChangeRequested;
	public event EventHandler? ConnectionClosed;
	public event EventHandler? ConnectionFailed;
	public event EventHandler<bool>? CanStartChanged;

	/// <summary>
	/// サーバーから緯度経度が配信されたときに発火する。
	/// 引数は (Longitude, Latitude, Accuracy?) の tuple。
	/// </summary>
	public event EventHandler<(double Longitude, double Latitude, double? Accuracy)>? LonLatLocationReceived;

	protected bool _IsDisposed;

	/// <summary>
	/// Get the latest synced data from the service
	/// </summary>
	protected abstract Task<SyncedData> GetSyncedDataAsync(CancellationToken token);

	/// <summary>
	/// サーバー情報を要求する。
	/// 結果は <see cref="ServerInfoUpdated"/> イベントで通知される。
	/// </summary>
	public virtual Task RequestServerInfoAsync(CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <summary>
	/// ダイヤ情報を要求する。
	/// </summary>
	/// <param name="diagramId">取得対象のダイヤID。null を渡すと現在のダイヤを取得する</param>
	/// <param name="cancellationToken">キャンセルトークン</param>
	/// <remarks>結果は <see cref="DiagramInfoUpdated"/> イベントで通知される。</remarks>
	public virtual Task RequestDiagramInfoAsync(string? diagramId = null, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <summary>
	/// Called when WorkGroupId property changes
	/// </summary>
	protected virtual void OnWorkGroupIdChanged(string? value) { }

	/// <summary>
	/// Called when WorkId property changes
	/// </summary>
	protected virtual void OnWorkIdChanged(string? value) { }

	/// <summary>
	/// Called when TrainId property changes
	/// </summary>
	protected virtual void OnTrainIdChanged(string? value) { }

	/// <summary>
	/// Poll for synced data. Used by HTTP implementation for periodic polling.
	/// WebSocket implementation handles updates via event-driven approach instead.
	/// </summary>
	public async Task TickAsync(CancellationToken? cancellationToken = null)
	{
		cancellationToken ??= CancellationToken.None;
		SyncedData result = await GetSyncedDataAsync(cancellationToken.Value);
		ProcessSyncedData(result);
	}

	/// <summary>
	/// Process synced data and update state. Called by TickAsync (HTTP) or directly (WebSocket).
	/// </summary>
	protected void ProcessSyncedData(SyncedData syncedData)
	{
		Logger.Debug("ProcessSyncedData: Location_m={0}, Time_ms={1}, CanStart={2}, Latitude_deg={3}, Longitude_deg={4}, Accuracy_m={5}",
			syncedData.Location_m, syncedData.Time_ms, syncedData.CanStart,
			syncedData.Latitude_deg, syncedData.Longitude_deg, syncedData.Accuracy_m);

		// 有効な緯度経度であるかを先に判定 (NaN は除外)
		bool hasLonLat =
			syncedData.Latitude_deg is double lat && !double.IsNaN(lat)
			&& syncedData.Longitude_deg is double lon && !double.IsNaN(lon);

		if (!double.IsNaN(syncedData.Location_m))
		{
			// Location_m が来ていれば従来通りそれで駅判定する。
			// lat/lon ベースの履歴は次回フォールバック時に新規初期化する。
			UpdateCurrentStationWithLocation(syncedData.Location_m);
			_lonLatStationDetector.Reset();
		}
		else if (hasLonLat)
		{
			// Location_m が NaN かつ緯度経度がある場合は緯度経度ベースで駅判定する。
			UpdateCurrentStationWithLonLat(syncedData.Longitude_deg!.Value, syncedData.Latitude_deg!.Value);
		}

		int currentTime_s = (int)(syncedData.Time_ms / 1000);
		if (CurrentTime_s != currentTime_s)
		{
			CurrentTime_s = currentTime_s;
			Logger.Debug("TimeChanged: {0} -> {1}", currentTime_s, CurrentTime_s);
			TimeChanged?.Invoke(this, CurrentTime_s);
		}

		CanStart = syncedData.CanStart;
		CanUseService = syncedData.CanStart;

		// 緯度経度が両方含まれていれば LonLatLocationReceived を発火する。
		// 値がない場合 (HTTP プロトコルや lat/lon 非対応サーバー) や NaN は無視する。
		if (hasLonLat)
		{
			LonLatLocationReceived?.Invoke(this, (syncedData.Longitude_deg!.Value, syncedData.Latitude_deg!.Value, syncedData.Accuracy_m));
		}
	}

	/// <summary>
	/// <see cref="SyncedData.Location_m"/> が NaN のときに緯度経度ベースで駅判定する。
	/// 距離履歴の平均を取るために、検出器の状態は ProcessSyncedData の連続呼び出し間で
	/// 保持する (毎回 Sync は呼ばない)。
	/// </summary>
	private void UpdateCurrentStationWithLonLat(double longitude_deg, double latitude_deg)
	{
		if (StaLocationInfo is null || !IsEnabled)
			return;

		bool changed = _lonLatStationDetector.UpdateWithLonLat(longitude_deg, latitude_deg);
		if (changed)
		{
			ForceSetLocationInfo(_lonLatStationDetector.CurrentStationIndex, _lonLatStationDetector.IsRunningToNextStation);
		}
	}

	void UpdateCurrentStationWithLocation(double location_m)
	{
		if (StaLocationInfo is null || !IsEnabled || double.IsNaN(location_m))
			return;

		bool isIn(double threshold1, double threshold2)
		{
			if (threshold1 < threshold2)
				return threshold1 <= location_m && location_m < threshold2;
			else
				return threshold2 <= location_m && location_m < threshold1;
		}

		// 距離が逆戻りする可能性は考えない
		for (int i = 0; i < StaLocationInfo.Length; i++)
		{
			double staLocation_m = StaLocationInfo[i].Location_m;
			double staNearbyRadius_m = StaLocationInfo[i].NearbyRadius_m;
			if (isIn(staLocation_m - staNearbyRadius_m, staLocation_m + staNearbyRadius_m))
			{
				if (i != CurrentStationIndex || IsRunningToNextStation)
					ForceSetLocationInfo(i, false);
				return;
			}
			else if (i != 0 && isIn(StaLocationInfo[i - 1].Location_m, staLocation_m))
			{
				if (i - 1 != CurrentStationIndex || !IsRunningToNextStation)
					ForceSetLocationInfo(i - 1, true);
				return;
			}
		}

		double distanceFromFirstStation = Math.Abs(location_m - StaLocationInfo[0].Location_m);
		double distanceFromLastStation = Math.Abs(location_m - StaLocationInfo[^1].Location_m);
		if (distanceFromFirstStation < distanceFromLastStation)
		{
			if (0 != CurrentStationIndex || IsRunningToNextStation)
				ForceSetLocationInfo(0, false);
		}
		else
		{
			if (StaLocationInfo.Length - 1 != CurrentStationIndex || IsRunningToNextStation)
				ForceSetLocationInfo(StaLocationInfo.Length - 1, false);
		}
	}

	protected void RaiseTimetableUpdated(TimetableData timetableData)
	{
		Logger.Info("RaiseTimetableUpdated: WorkGroupId={0}, WorkId={1}, TrainId={2}, Scope={3}", timetableData.WorkGroupId, timetableData.WorkId, timetableData.TrainId, timetableData.Scope);
		// 時刻表の変更スコープに応じて、現在の位置情報をリセットすべきか判定する。
		// リアルタイム編集対応のため、自スコープの一致更新では位置情報を維持する。
		bool shouldResetLocation = ShouldResetLocationOnTimetableUpdate(timetableData);

		if (shouldResetLocation)
		{
			// 全体更新など影響範囲が大きい場合のみ初期状態に戻す
			Logger.Warn("RaiseTimetableUpdated: Resetting location info due to global scope update");
			ResetLocationInfo();
		}

		// 時刻表更新イベントを外部に通知
		TimetableUpdated?.Invoke(this, timetableData);
	}

	protected void RaiseServerInfoUpdated(ServerInfo serverInfo)
	{
		Logger.Info("RaiseServerInfoUpdated: Name={0}, Version={1}", serverInfo.Name, serverInfo.Version);
		ServerInfoUpdated?.Invoke(this, serverInfo);
	}

	protected void RaiseDiagramInfoUpdated(DiagramInfo diagramInfo)
	{
		Logger.Info("RaiseDiagramInfoUpdated: Id={0}, Name={1}", diagramInfo.Id, diagramInfo.Name);
		DiagramInfoUpdated?.Invoke(this, diagramInfo);
	}

	protected void RaiseTrainSelectionRequested(SelectTrainCommand command)
	{
		Logger.Info("RaiseTrainSelectionRequested: WorkGroupId={0}, WorkId={1}, TrainId={2}",
			command.WorkGroupId, command.WorkId, command.TrainId);
		TrainSelectionRequested?.Invoke(this, command);
	}

	protected void RaiseOperationCommandReceived(OperationCommand command)
	{
		Logger.Info("RaiseOperationCommandReceived: Action={0}", command.Action);
		OperationCommandReceived?.Invoke(this, command);
	}

	protected void RaiseHeaderColorChangeRequested(HeaderColorCommand command)
	{
		Logger.Info("RaiseHeaderColorChangeRequested: ResetToDefault={0}, Color_RGB={1}",
			command.ResetToDefault, command.Color_RGB);
		HeaderColorChangeRequested?.Invoke(this, command);
	}

	protected void RaiseNotificationReceived(NotificationData notification)
	{
		Logger.Info("RaiseNotificationReceived: Id={0}, Title={1}", notification.Id, notification.Title);
		NotificationReceived?.Invoke(this, notification);
	}

	protected void RaiseTimeFormatChangeRequested(TimeFormatCommand command)
	{
		Logger.Info("RaiseTimeFormatChangeRequested: Format={0}", command.Format);
		TimeFormatChangeRequested?.Invoke(this, command);
	}

	protected void RaiseConnectionClosed()
	{
		ConnectionClosed?.Invoke(this, EventArgs.Empty);
	}

	protected void RaiseConnectionFailed()
	{
		ConnectionFailed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// 時刻表の更新を受信したとき、位置情報を初期状態にリセットすべきかを判定する。
	/// リアルタイム編集対応のため、自スコープと一致する更新では位置情報を維持する
	/// (例: 編集中の Train が更新されても駅 index を保持する)。
	/// 全体更新 (<see cref="TimetableScopeType.All"/>) の場合も、現在追跡中の Train が
	/// 新ペイロードに残っていれば維持する (#245: 運行中の意図しないリセットを防ぐ)。
	/// </summary>
	private bool ShouldResetLocationOnTimetableUpdate(TimetableData timetableData)
	{
		return timetableData.Scope switch
		{
			// 全体更新: 現在追跡中の Train が新ペイロードに残っていれば維持する。
			//   - 残っている → 編集や再配信のたびに運行中状態を巻き戻されては困るので維持
			//   - 残っていない (削除された or 未選択) → 古い駅 index が新時刻表で有効な保証がないのでリセット
			TimetableScopeType.All => !IsCurrentTrainStillTracked(),
			// WorkGroup / Work / Train 単位の更新: 自スコープと一致するか否かに関わらず維持する。
			//   - 一致する場合 → ユーザーが今見ているデータの再描画を期待しているのでリセットしない
			//   - 一致しない場合 → そもそも現在の表示と無関係なのでリセットしない
			TimetableScopeType.WorkGroup => false,
			TimetableScopeType.Work => false,
			TimetableScopeType.Train => false,
			_ => false,
		};
	}

	/// <summary>
	/// 現在 <see cref="TrainId"/> として追跡している列車が、最新の時刻表データに依然存在しているかを返す。
	/// <see cref="ShouldResetLocationOnTimetableUpdate"/> が Scope.All 受信時の判定に使う。
	/// 既定では false (= リセット側に倒す) を返す。時刻表キャッシュにアクセスできる実装側で
	/// override すること (例: WebSocketNetworkSyncService)。
	/// </summary>
	protected virtual bool IsCurrentTrainStillTracked() => false;

	public void ForceSetLocationInfo(
		int stationIndex,
		bool isRunningToNextStation
	)
	{
		CurrentStationIndex = stationIndex;
		IsRunningToNextStation = isRunningToNextStation;
		// 緯度経度ベースの駅判定エンジンも外部の強制セットに合わせて状態同期する。
		// (距離履歴は外部介入の時点で意味を失うのでクリアする)
		_lonLatStationDetector.Sync(stationIndex, isRunningToNextStation);
		LocationStateChanged?.Invoke(this, new LocationStateChangedEventArgs(CurrentStationIndex, IsRunningToNextStation));
	}

	public void ResetLocationInfo()
	{
		CurrentStationIndex = 0;
		IsRunningToNextStation = false;
		_lonLatStationDetector.Reset();
		LocationStateChanged?.Invoke(this, new LocationStateChangedEventArgs(CurrentStationIndex, IsRunningToNextStation));
	}

	public abstract void Dispose();
}
