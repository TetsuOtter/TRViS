using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using NLog;

using TRViS.IO;
using TRViS.IO.Models;

using JsonModels = TRViS.JsonModels;

namespace TRViS.NetworkSyncService;

/// <summary>
/// WebSocket-based implementation of NetworkSyncService
/// </summary>
public class WebSocketNetworkSyncService : NetworkSyncServiceBase, ILoader
{
	private static readonly Logger logger = LogManager.GetCurrentClassLogger();

	// SyncedDataメッセージのJSONキー
	private const string LOCATION_M_JSON_KEY = "Location_m";
	private const string TIME_MS_JSON_KEY = "Time_ms";
	private const string CAN_START_JSON_KEY = "CanStart";

	// ID更新メッセージのJSONキー
	private const string WORK_GROUP_ID_JSON_KEY = "WorkGroupId";
	private const string WORK_ID_JSON_KEY = "WorkId";
	private const string TRAIN_ID_JSON_KEY = "TrainId";

	// 時刻表メッセージのJSONキー
	private const string MESSAGE_TYPE_JSON_KEY = "MessageType";
	private const string MESSAGE_TYPE_SYNCED_DATA = "SyncedData";
	private const string MESSAGE_TYPE_TIMETABLE = "Timetable";
	private const string TIMETABLE_DATA_JSON_KEY = "Data";

	private ClientWebSocket _WebSocket;
	private readonly Uri _Uri;
	private readonly byte[] _ReceiveBuffer = new byte[4096];
	private SyncedData _LatestData = new(double.NaN, 0, false);
	private CancellationTokenSource? _ReceiveLoopCts;
	private Task? _ReceiveLoopTask;

	// 再接続管理用
	private const int RECONNECT_ATTEMPT_MAX = 3;  // 最大再接続試行回数
	private const int RECONNECT_INTERVAL_MS = 5000;  // 再接続間隔（5秒）
	private const int KEEP_ALIVE_INTERVAL_MS = 10000;  // ハートビート間隔（10秒）
	private const int KEEP_ALIVE_TIMEOUT_MS = 15000;  // ハートビート応答タイムアウト（15秒）

	// JSONデシリアライズ用のオプション
	private static readonly JsonSerializerOptions JsonDeserializeOptions = new()
	{
		AllowTrailingCommas = true,
		PropertyNameCaseInsensitive = true,
	};

	// ILoader実装用のキャッシュ
	private readonly Dictionary<string, WorkGroup> _WorkGroupCache = [];
	private readonly Dictionary<string, List<Work>> _WorkListCache = [];
	private readonly Dictionary<string, TrainData> _TrainDataCache = [];
	private readonly Dictionary<string, List<TrainData>> _TrainListByWorkIdCache = [];

	public WebSocketNetworkSyncService(Uri uri, ClientWebSocket webSocket)
	{
		_Uri = uri;
		_WebSocket = webSocket;
		logger.Info("WebSocketNetworkSyncService created with URI: {0}", uri);
	}

	public async Task ConnectAsync(CancellationToken cancellationToken)
	{
		if (_WebSocket.State == WebSocketState.Open)
		{
			logger.Warn("ConnectAsync: WebSocket is already open");
			return;
		}

		logger.Info("ConnectAsync: Connecting to {0}", _Uri);
		ConfigureWebSocketOptions(_WebSocket);
		await _WebSocket.ConnectAsync(_Uri, cancellationToken);
		logger.Info("ConnectAsync: Connected successfully");
		StartReceiveLoop();
	}

	private static void ConfigureWebSocketOptions(ClientWebSocket webSocket)
	{
		// KeepAlive設定を適用（OS/フレームワークレベルでのハートビート）
		webSocket.Options.KeepAliveInterval = TimeSpan.FromMilliseconds(KEEP_ALIVE_INTERVAL_MS);
		webSocket.Options.KeepAliveTimeout = TimeSpan.FromMilliseconds(KEEP_ALIVE_TIMEOUT_MS);
	}

	private void StartReceiveLoop()
	{
		_ReceiveLoopCts = new CancellationTokenSource();
		_ReceiveLoopTask = ReceiveLoopAsync(_ReceiveLoopCts.Token);
	}

	private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
	{
		int reconnectAttempt = 0;
		bool shouldExit = false;

		while (!cancellationToken.IsCancellationRequested && !shouldExit)
		{
			bool shouldRaiseConnectionClosed = false;
			bool shouldRaiseConnectionFailed = false;

			try
			{
				reconnectAttempt = await ReceiveMessagesAsync(reconnectAttempt, cancellationToken);
				shouldRaiseConnectionClosed = true;
			}
			catch (OperationCanceledException)
			{
				logger.Info("ReceiveLoopAsync: Cancelled");
				shouldRaiseConnectionClosed = true;
				// Expected when cancellation is requested
			}
			catch (Exception ex)
			{
				logger.Error(ex, "ReceiveLoopAsync: WebSocket exception");
				// 接続が切断された場合、再接続を試みる
				int result = await AttemptReconnectAsync(reconnectAttempt, cancellationToken);
				if (result < 0)
				{
					logger.Warn("ReceiveLoopAsync: Failed to reconnect after {0} attempts", RECONNECT_ATTEMPT_MAX);
					shouldRaiseConnectionFailed = true;
					shouldExit = true;
				}
				else
				{
					// 再接続成功時は次のループを継続
					logger.Info("ReceiveLoopAsync: Reconnected successfully, restarting receive loop");
					continue;
				}
			}
			finally
			{
				// イベントを発火（再接続失敗と正常終了は相互排他的）
				if (shouldRaiseConnectionFailed)
				{
					logger.Info("ReceiveLoopAsync: Connection failed");
					RaiseConnectionFailed();
					shouldExit = true;
				}
				else if (shouldRaiseConnectionClosed)
				{
					logger.Info("ReceiveLoopAsync: Connection closed");
					RaiseConnectionClosed();
					shouldExit = true;
				}
			}
		}
	}

	private async Task<int> ReceiveMessagesAsync(int reconnectAttempt, CancellationToken cancellationToken)
	{
		StringBuilder messageBuilder = new();

		while (!cancellationToken.IsCancellationRequested && _WebSocket.State == WebSocketState.Open)
		{
			WebSocketReceiveResult result = await _WebSocket.ReceiveAsync(
				new ArraySegment<byte>(_ReceiveBuffer),
				cancellationToken
			);

			if (result.MessageType == WebSocketMessageType.Close)
			{
				logger.Info("ReceiveMessagesAsync: Received Close message from server");
				await _WebSocket.CloseAsync(
					WebSocketCloseStatus.NormalClosure,
					"Closing",
					CancellationToken.None
				);
				break;
			}

			if (result.MessageType == WebSocketMessageType.Text)
			{
				messageBuilder.Append(Encoding.UTF8.GetString(_ReceiveBuffer, 0, result.Count));

				if (result.EndOfMessage)
				{
					string message = messageBuilder.ToString();
					messageBuilder.Clear();
					logger.Debug("ReceiveMessagesAsync: Received message: {0}", message);
					ProcessMessage(message);
					reconnectAttempt = 0;  // メッセージ受信成功時は再接続カウントをリセット
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();

		return reconnectAttempt;
	}

	private void ProcessMessage(string message)
	{
		try
		{
			using JsonDocument? json = JsonDocument.Parse(message);
			if (json is null)
			{
				logger.Warn("ProcessMessage: Failed to parse JSON");
				return;
			}

			JsonElement root = json.RootElement;

			// メッセージタイプを確認
			string? messageType = null;
			try
			{
				messageType = root.GetProperty(MESSAGE_TYPE_JSON_KEY).GetString();
			}
			catch (KeyNotFoundException) { }

			logger.Debug("ProcessMessage: Message type: {0}", messageType ?? "null");

			if (messageType == MESSAGE_TYPE_SYNCED_DATA)
			{
				ProcessSyncedDataMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_TIMETABLE)
			{
				ProcessTimetableMessage(root);
			}
		}
		catch (JsonException ex)
		{
			logger.Error(ex, "ProcessMessage: Invalid JSON");
			// Invalid JSON, ignore
		}
	}

	private void ProcessSyncedDataMessage(JsonElement root)
	{
		double location_m = double.NaN;
		try
		{
			JsonElement location_m_element = root.GetProperty(LOCATION_M_JSON_KEY);
			if (location_m_element.ValueKind == JsonValueKind.Null)
				location_m = double.NaN;
			else
				location_m = location_m_element.GetDouble();
		}
		catch (KeyNotFoundException) { }
		catch (FormatException) { }

		long time_ms = 0;
		try
		{
			time_ms = root.GetProperty(TIME_MS_JSON_KEY).GetInt64();
		}
		catch (KeyNotFoundException) { }
		catch (FormatException) { }

		bool canStart = true;
		try
		{
			canStart = root.GetProperty(CAN_START_JSON_KEY).GetBoolean();
		}
		catch (KeyNotFoundException) { }
		catch (FormatException) { }

		SyncedData syncedData = new SyncedData(location_m, time_ms, canStart);
		_LatestData = syncedData;

		// WebSocket uses event-driven approach: process data immediately upon receipt
		ProcessSyncedData(syncedData);
	}

	private void ProcessTimetableMessage(JsonElement root)
	{
		var timetableData = new TimetableData();

		// WorkGroupId, WorkId, TrainIdを取得
		try
		{
			if (root.TryGetProperty(WORK_GROUP_ID_JSON_KEY, out var wgId))
				timetableData.WorkGroupId = wgId.GetString();
		}
		catch (FormatException) { }

		try
		{
			if (root.TryGetProperty(WORK_ID_JSON_KEY, out var wId))
				timetableData.WorkId = wId.GetString();
		}
		catch (FormatException) { }

		try
		{
			if (root.TryGetProperty(TRAIN_ID_JSON_KEY, out var tId))
				timetableData.TrainId = tId.GetString();
		}
		catch (FormatException) { }

		// スコープを取得（WorkGroup > Work > Train の優先度で判定）
		if (timetableData.WorkGroupId is not null)
		{
			timetableData.Scope = TimetableScopeType.WorkGroup;
		}
		else if (timetableData.WorkId is not null)
		{
			timetableData.Scope = TimetableScopeType.Work;
		}
		else if (timetableData.TrainId is not null)
		{
			timetableData.Scope = TimetableScopeType.Train;
		}
		else
		{
			timetableData.Scope = TimetableScopeType.All;
		}

		// 時刻表JSONデータを取得
		try
		{
			if (root.TryGetProperty(TIMETABLE_DATA_JSON_KEY, out var data))
			{
				timetableData.JsonData = data.GetRawText();
				CacheTimetableData(timetableData);
			}
		}
		catch (FormatException) { }

		// イベントを発火
		RaiseTimetableUpdated(timetableData);
	}

	private void CacheTimetableData(TimetableData timetableData)
	{
		try
		{
			using JsonDocument? json = JsonDocument.Parse(timetableData.JsonData);
			if (json is null)
				return;

			JsonElement dataElement = json.RootElement;

			// スコープに応じてキャッシュを更新
			switch (timetableData.Scope)
			{
				case TimetableScopeType.All:
					// 全体の情報の場合は、ローカルキャッシュを全てリセットして、新しいデータで再構築する
					logger.Info("CacheTimetableData: Resetting and rebuilding all cache due to All scope update");
					_WorkGroupCache.Clear();
					_WorkListCache.Clear();
					_TrainDataCache.Clear();
					_TrainListByWorkIdCache.Clear();

					// JsonModelsを使ってデシリアライズ
					try
					{
						var workGroups = JsonSerializer.Deserialize<JsonModels.WorkGroupData[]>(
							timetableData.JsonData,
							JsonDeserializeOptions
						);

						if (workGroups is not null)
						{
							foreach (var workGroupData in workGroups)
							{
								CacheConvertedWorkGroup(workGroupData);
							}
						}
					}
					catch (JsonException ex)
					{
						logger.Error(ex, "CacheTimetableData: Failed to deserialize WorkGroup array");
					}
					break;
				case TimetableScopeType.WorkGroup:
					if (timetableData.WorkGroupId is not null)
					{
						// WorkGroupの情報をキャッシュ
						var jsonModels = JsonSerializer.Deserialize<JsonModels.WorkGroupData>(
							timetableData.JsonData,
							JsonDeserializeOptions
						);
						var workGroup = JsonModelsConverter.ConvertWorkGroup(jsonModels!);
						_WorkGroupCache[timetableData.WorkGroupId] = workGroup;
					}
					break;

				case TimetableScopeType.Work:
					if (timetableData.WorkId is not null && timetableData.WorkGroupId is not null)
					{
						// Workの情報をキャッシュ
						var jsonModels = JsonSerializer.Deserialize<JsonModels.WorkData>(
							timetableData.JsonData,
							JsonDeserializeOptions
						);
						var work = JsonModelsConverter.ConvertWork(jsonModels!, timetableData.WorkGroupId);

						if (!_WorkListCache.ContainsKey(timetableData.WorkGroupId))
							_WorkListCache[timetableData.WorkGroupId] = [];

						// 既存のWorkを削除して追加（更新）
						_WorkListCache[timetableData.WorkGroupId].RemoveAll(w => w.Id == timetableData.WorkId);
						_WorkListCache[timetableData.WorkGroupId].Add(work);
					}
					break;

				case TimetableScopeType.Train:
					if (timetableData.TrainId is not null)
					{
						// TrainDataの情報をキャッシュ
						var jsonModels = JsonSerializer.Deserialize<JsonModels.TrainData>(
							timetableData.JsonData,
							JsonDeserializeOptions
						);
						var trainData = JsonModelsConverter.ConvertTrain(jsonModels!);
						_TrainDataCache[timetableData.TrainId] = trainData;

						// WorkIdに紐づくTrainのリストにも追加
						if (timetableData.WorkId is not null)
						{
							if (!_TrainListByWorkIdCache.ContainsKey(timetableData.WorkId))
								_TrainListByWorkIdCache[timetableData.WorkId] = [];

							// 既存のTrainDataを削除して追加（更新）
							_TrainListByWorkIdCache[timetableData.WorkId].RemoveAll(t => t.Id == timetableData.TrainId);
							_TrainListByWorkIdCache[timetableData.WorkId].Add(trainData);
						}
					}
					break;
			}
		}
		catch (JsonException)
		{
			// Invalid JSON, ignore
		}
	}

	private void CacheConvertedWorkGroup(JsonModels.WorkGroupData workGroupData)
	{
		try
		{
			// JsonModelsConverterを使用してWorkGroupを変換
			var workGroup = JsonModelsConverter.ConvertWorkGroup(workGroupData);
			_WorkGroupCache[workGroup.Id] = workGroup;
			logger.Debug("CacheConvertedWorkGroup: Added WorkGroup {0} ({1})", workGroup.Id, workGroup.Name);

			// Works配列を処理
			if (workGroupData.Works is not null && workGroupData.Works.Length > 0)
			{
				if (!_WorkListCache.ContainsKey(workGroup.Id))
					_WorkListCache[workGroup.Id] = [];

				foreach (var workData in workGroupData.Works)
				{
					CacheConvertedWork(workData, workGroup.Id);
				}
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "CacheConvertedWorkGroup: Failed to process WorkGroup");
		}
	}

	private void CacheConvertedWork(JsonModels.WorkData workData, string workGroupId)
	{
		try
		{
			// JsonModelsConverterを使用してWorkを変換
			var works = JsonModelsConverter.ConvertWorks(new[] { workData }, workGroupId);
			if (works.Length > 0)
			{
				var work = works[0];
				_WorkListCache[workGroupId].Add(work);
				logger.Debug("CacheConvertedWork: Added Work {0} ({1})", work.Id, work.Name);

				// Trains配列を処理
				if (workData.Trains is not null && workData.Trains.Length > 0)
				{
					if (!_TrainListByWorkIdCache.ContainsKey(work.Id))
						_TrainListByWorkIdCache[work.Id] = [];

					foreach (var trainData in workData.Trains)
					{
						CacheConvertedTrain(trainData, work.Id);
					}
				}
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "CacheConvertedWork: Failed to process Work");
		}
	}

	private void CacheConvertedTrain(JsonModels.TrainData trainDataJson, string workId)
	{
		try
		{
			// JsonModelsConverterを使用してTrainDataを変換
			var trainData = JsonModelsConverter.ConvertTrain(trainDataJson);
			_TrainDataCache[trainData.Id] = trainData;
			_TrainListByWorkIdCache[workId].Add(trainData);
			logger.Debug("CacheConvertedTrain: Added Train {0} ({1})", trainData.Id, trainData.TrainNumber);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "CacheConvertedTrain: Failed to process Train");
		}
	}

	protected override void OnWorkGroupIdChanged(string? value)
	{
		logger.Debug("OnWorkGroupIdChanged: {0}", value);
		_ = SendIdUpdateAsync();
	}

	protected override void OnWorkIdChanged(string? value)
	{
		logger.Debug("OnWorkIdChanged: {0}", value);
		_ = SendIdUpdateAsync();
	}

	protected override void OnTrainIdChanged(string? value)
	{
		logger.Debug("OnTrainIdChanged: {0}", value);
		_ = SendIdUpdateAsync();
	}

	private async Task SendIdUpdateAsync()
	{
		if (_WebSocket.State != WebSocketState.Open)
		{
			logger.Warn("SendIdUpdateAsync: WebSocket is not open");
			return;
		}

		try
		{
			var updateMessage = new Dictionary<string, string?>();
			if (WorkGroupId is not null)
				updateMessage[WORK_GROUP_ID_JSON_KEY] = WorkGroupId;
			if (WorkId is not null)
				updateMessage[WORK_ID_JSON_KEY] = WorkId;
			if (TrainId is not null)
				updateMessage[TRAIN_ID_JSON_KEY] = TrainId;

			string json = JsonSerializer.Serialize(updateMessage);
			logger.Debug("SendIdUpdateAsync: Sending ID update: {0}", json);
			byte[] bytes = Encoding.UTF8.GetBytes(json);
			await _WebSocket.SendAsync(
				new ArraySegment<byte>(bytes),
				WebSocketMessageType.Text,
				endOfMessage: true,
				CancellationToken.None
			);
		}
		catch (WebSocketException ex)
		{
			logger.Error(ex, "SendIdUpdateAsync: WebSocket exception");
			// Connection closed or error occurred
		}
	}

	protected override Task<SyncedData> GetSyncedDataAsync(CancellationToken token)
	{
		// WebSocket is event-driven, return the latest cached data
		// This method is not used by WebSocket implementation
		return Task.FromResult(_LatestData);
	}

	/// <summary>
	/// ILoader実装: 指定のTrainIdのTrainDataを取得します
	/// </summary>
	public TrainData? GetTrainData(string trainId)
	{
		_TrainDataCache.TryGetValue(trainId, out var trainData);
		return trainData;
	}

	/// <summary>
	/// ILoader実装: キャッシュされたWorkGroupのリストを取得します
	/// </summary>
	public IReadOnlyList<WorkGroup> GetWorkGroupList()
	{
		return _WorkGroupCache.Values.ToList();
	}

	/// <summary>
	/// ILoader実装: 指定のWorkGroupIdに属するWorkのリストを取得します
	/// </summary>
	public IReadOnlyList<Work> GetWorkList(string workGroupId)
	{
		if (_WorkListCache.TryGetValue(workGroupId, out var workList))
			return workList.AsReadOnly();

		return new List<Work>();
	}

	/// <summary>
	/// ILoader実装: 指定のWorkIdに属するTrainDataのリストを取得します
	/// </summary>
	public IReadOnlyList<TrainData> GetTrainDataList(string workId)
	{
		if (_TrainListByWorkIdCache.TryGetValue(workId, out var trainList))
			return trainList.AsReadOnly();

		return new List<TrainData>();
	}

	private async Task<int> AttemptReconnectAsync(int reconnectAttempt, CancellationToken cancellationToken)
	{
		logger.Info("AttemptReconnectAsync: Starting reconnection attempts (max: {0})", RECONNECT_ATTEMPT_MAX);

		while (reconnectAttempt < RECONNECT_ATTEMPT_MAX && !cancellationToken.IsCancellationRequested)
		{
			reconnectAttempt++;
			logger.Info("AttemptReconnectAsync: Attempt {0}/{1}", reconnectAttempt, RECONNECT_ATTEMPT_MAX);

			try
			{
				// 再接続間隔を待つ
				await Task.Delay(RECONNECT_INTERVAL_MS, cancellationToken);

				// WebSocketが閉じられていれば新しいものを作成
				if (_WebSocket.State != WebSocketState.Open && _WebSocket.State != WebSocketState.Connecting)
				{
					logger.Info("AttemptReconnectAsync: Creating new WebSocket");
					_WebSocket.Dispose();
					// WebSocketは再利用できないため、新しいインスタンスを作成する
					_WebSocket = new ClientWebSocket();
					ConfigureWebSocketOptions(_WebSocket);
				}

				// 再接続を試みる
				logger.Info("AttemptReconnectAsync: Reconnecting to {0}", _Uri);
				await _WebSocket.ConnectAsync(_Uri, cancellationToken);

				logger.Info("AttemptReconnectAsync: Successfully reconnected on attempt {0}", reconnectAttempt);

				// Receive と Ping ループを再開
				StartReceiveLoop();
				return reconnectAttempt;  // 再接続成功
			}
			catch (OperationCanceledException)
			{
				logger.Info("AttemptReconnectAsync: Cancelled");
				return -1;
			}
			catch (WebSocketException ex)
			{
				logger.Warn(ex, "AttemptReconnectAsync: Reconnection attempt {0} failed", reconnectAttempt);
				if (reconnectAttempt < RECONNECT_ATTEMPT_MAX)
				{
					logger.Info("AttemptReconnectAsync: Retrying in {0}ms", RECONNECT_INTERVAL_MS);
				}
			}
			catch (Exception ex)
			{
				logger.Error(ex, "AttemptReconnectAsync: Unexpected exception during reconnection attempt {0}", reconnectAttempt);
				if (reconnectAttempt < RECONNECT_ATTEMPT_MAX)
				{
					logger.Info("AttemptReconnectAsync: Retrying in {0}ms", RECONNECT_INTERVAL_MS);
				}
			}
		}

		logger.Warn("AttemptReconnectAsync: All reconnection attempts failed");
		return -1;  // 再接続失敗
	}

	private async Task ForceDisconnectAsync()
	{
		logger.Warn("ForceDisconnectAsync: Forcing disconnect");
		try
		{
			if (_WebSocket.State == WebSocketState.Open)
			{
				await _WebSocket.CloseAsync(
					WebSocketCloseStatus.InternalServerError,
					"Pong timeout",
					CancellationToken.None
				);
			}
		}
		catch (WebSocketException ex)
		{
			logger.Warn(ex, "ForceDisconnectAsync: WebSocket already closed");
			// Already closed
		}

		// ReceiveLoopを強制終了
		_ReceiveLoopCts?.Cancel();
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken = default)
	{
		logger.Info("DisconnectAsync: Disconnecting");
		_ReceiveLoopCts?.Cancel();

		if (_WebSocket.State == WebSocketState.Open)
		{
			try
			{
				await _WebSocket.CloseAsync(
					WebSocketCloseStatus.NormalClosure,
					"Client disconnecting",
					cancellationToken
				);
			}
			catch (WebSocketException ex)
			{
				logger.Warn(ex, "DisconnectAsync: WebSocket exception");
				// Already closed or error
			}
		}

		if (_ReceiveLoopTask is not null)
		{
			try
			{
				await _ReceiveLoopTask;
			}
			catch (OperationCanceledException)
			{
				logger.Debug("DisconnectAsync: ReceiveLoop cancelled");
				// Expected
			}
		}
		logger.Info("DisconnectAsync: Disconnected");
	}

	public override void Dispose()
	{
		if (_IsDisposed)
			return;

		logger.Info("Dispose: Disposing WebSocketNetworkSyncService");
		_IsDisposed = true;
		_ReceiveLoopCts?.Cancel();
		_ReceiveLoopCts?.Dispose();
		_WebSocket.Dispose();
	}
}
