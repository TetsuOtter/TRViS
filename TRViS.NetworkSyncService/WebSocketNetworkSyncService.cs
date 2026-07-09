using System;
using System.Collections.Concurrent;
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
	private const string LATITUDE_DEG_JSON_KEY = "Latitude_deg";
	private const string LONGITUDE_DEG_JSON_KEY = "Longitude_deg";
	private const string ACCURACY_M_JSON_KEY = "Accuracy_m";

	// ID更新メッセージのJSONキー
	private const string WORK_GROUP_ID_JSON_KEY = "WorkGroupId";
	private const string WORK_ID_JSON_KEY = "WorkId";
	private const string TRAIN_ID_JSON_KEY = "TrainId";

	// 時刻表メッセージのJSONキー
	private const string MESSAGE_TYPE_JSON_KEY = "MessageType";
	private const string MESSAGE_TYPE_SYNCED_DATA = "SyncedData";
	private const string MESSAGE_TYPE_TIMETABLE = "Timetable";
	private const string MESSAGE_TYPE_SERVER_INFO = "ServerInfo";
	private const string MESSAGE_TYPE_DIAGRAM_INFO = "DiagramInfo";
	private const string MESSAGE_TYPE_REQUEST_SERVER_INFO = "RequestServerInfo";
	private const string MESSAGE_TYPE_REQUEST_DIAGRAM_INFO = "RequestDiagramInfo";
	private const string MESSAGE_TYPE_ACKNOWLEDGE_NOTIFICATION = "AcknowledgeNotification";
	private const string MESSAGE_TYPE_SELECT_TRAIN = "SelectTrain";
	private const string MESSAGE_TYPE_OPERATION_COMMAND = "OperationCommand";
	private const string MESSAGE_TYPE_HEADER_COLOR = "HeaderColor";
	private const string MESSAGE_TYPE_NOTIFICATION = "Notification";
	private const string MESSAGE_TYPE_TIME_FORMAT = "TimeFormat";
	private const string MESSAGE_TYPE_NAVIGATE_TO_HOME = "NavigateToHome";
	private const string MESSAGE_TYPE_OPEN_TIMETABLE = "OpenTimetable";
	private const string TIMETABLE_DATA_JSON_KEY = "Data";

	// 列車検索 (v1.1) のJSONキー
	private const string MESSAGE_TYPE_SEARCH_TRAIN = "SearchTrain";
	private const string MESSAGE_TYPE_SEARCH_TRAIN_RESPONSE = "SearchTrainResponse";
	private const string MESSAGE_TYPE_REQUEST_TRAIN_TIMETABLE = "RequestTrainTimetable";
	private const string REQUEST_ID_JSON_KEY = "RequestId";
	private const string TRAIN_NUMBER_JSON_KEY = "TrainNumber";
	private const string MATCH_MODE_JSON_KEY = "MatchMode";
	private const string SEARCH_RESULTS_JSON_KEY = "Results";
	private const string SERVER_FEATURES_JSON_KEY = "Features";
	private const string WORK_NAME_JSON_KEY = "WorkName";
	private const string DIRECTION_JSON_KEY = "Direction";
	private const string START_STATION_NAME_JSON_KEY = "StartStationName";
	private const string START_TIME_JSON_KEY = "StartTime";
	private const string END_STATION_NAME_JSON_KEY = "EndStationName";
	private const string END_TIME_JSON_KEY = "EndTime";

	// 列車検索のタイムアウト既定値 (Issue #197: 応答が無い場合にタイムアウトが働くこと)
	private const int DEFAULT_SEARCH_TRAIN_TIMEOUT_MS = 10000;
	private const int DEFAULT_FETCH_TIMETABLE_TIMEOUT_MS = 15000;

	// ServerInfo / DiagramInfo のJSONキー
	private const string SERVER_NAME_JSON_KEY = "Name";
	private const string SERVER_ADMIN_JSON_KEY = "Admin";
	private const string SERVER_VERSION_JSON_KEY = "Version";
	private const string SERVER_PROTOCOL_VERSION_JSON_KEY = "ProtocolVersion";
	private const string DIAGRAM_ID_JSON_KEY = "DiagramId";
	private const string DIAGRAM_NAME_JSON_KEY = "Name";
	private const string DIAGRAM_DESCRIPTION_JSON_KEY = "Description";
	private const string DIAGRAM_WORK_GROUP_IDS_JSON_KEY = "WorkGroupIds";

	private ClientWebSocket _WebSocket;
	private readonly Uri _Uri;
	private readonly byte[] _ReceiveBuffer = new byte[4096];
	private SyncedData _LatestData = new(double.NaN, 0, false);
	private CancellationTokenSource? _ReceiveLoopCts;
	private Task? _ReceiveLoopTask;
	private volatile bool _isDisconnecting = false;

	// ClientWebSocket は同時に 1 つの SendAsync しか許さない。ID 更新 / 各種 Request
	// (ServerInfo / DiagramInfo) / 再接続後の再送が fire-and-forget で重なり得るため、
	// すべての送信をこのセマフォで直列化する。
	private readonly SemaphoreSlim _sendLock = new(1, 1);

	// 再接続管理用
	private readonly int _reconnectAttemptMax;  // 最大再接続試行回数
	private readonly int _reconnectIntervalMs;  // 再接続間隔
	private const int KEEP_ALIVE_INTERVAL_MS = 10000;  // ハートビート間隔（10秒）
	private const int KEEP_ALIVE_TIMEOUT_MS = 15000;  // ハートビート応答タイムアウト（15秒）

	// JSONデシリアライズ用のオプション
	private static readonly JsonSerializerOptions JsonDeserializeOptions = new()
	{
		AllowTrailingCommas = true,
		PropertyNameCaseInsensitive = true,
	};

	// 列車検索 (SearchTrain) の応答を RequestId で相関させるための待機辞書。
	// 受信ループ (バックグラウンド) が応答受信時に該当 TCS を完了させる。
	private readonly ConcurrentDictionary<string, TaskCompletionSource<IReadOnlyList<TrainSearchResult>>>
		_pendingSearches = new();

	/// <summary>列車検索 (<see cref="SearchTrainAsync"/>) のタイムアウト [ms]。</summary>
	public int SearchTrainTimeoutMs { get; set; } = DEFAULT_SEARCH_TRAIN_TIMEOUT_MS;

	/// <summary>時刻表取得 (<see cref="FetchSearchedTrainTimetableAsync"/>) のタイムアウト [ms]。</summary>
	public int FetchTrainTimetableTimeoutMs { get; set; } = DEFAULT_FETCH_TIMETABLE_TIMEOUT_MS;

	// ILoader実装用のキャッシュ
	private readonly Dictionary<string, WorkGroup> _WorkGroupCache = [];
	private readonly Dictionary<string, List<Work>> _WorkListCache = [];
	private readonly Dictionary<string, TrainData> _TrainDataCache = [];
	private readonly Dictionary<string, List<TrainData>> _TrainListByWorkIdCache = [];

	public WebSocketNetworkSyncService(Uri uri, ClientWebSocket webSocket,
		int reconnectIntervalMs = 5000, int reconnectAttemptMax = 3)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reconnectIntervalMs);
		ArgumentOutOfRangeException.ThrowIfNegative(reconnectAttemptMax);
		_Uri = uri;
		_WebSocket = webSocket;
		_reconnectIntervalMs = reconnectIntervalMs;
		_reconnectAttemptMax = reconnectAttemptMax;
		logger.Info("WebSocketNetworkSyncService created with URI: {0}", uri);
	}

	public async Task ConnectAsync(CancellationToken cancellationToken)
	{
		if (_WebSocket.State == WebSocketState.Open)
		{
			logger.Warn("ConnectAsync: WebSocket is already open");
			return;
		}

		_isDisconnecting = false;
		logger.Info("ConnectAsync: Connecting to {0}", _Uri);
		ConfigureWebSocketOptions(_WebSocket);
		await _WebSocket.ConnectAsync(_Uri, cancellationToken);
		logger.Info("ConnectAsync: Connected successfully");
		StartReceiveLoop();

		// 接続直後にサーバー情報を要求し、対応機能 (ServerInfo.Features) を取得する。
		// fire-and-forget: 応答は ServerInfoUpdated / ServerFeatures に反映される。
		// 非対応サーバーは応答しないだけで害はない。
		_ = RequestServerInfoAsync(CancellationToken.None);
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

		while (!cancellationToken.IsCancellationRequested)
		{
			bool lostConnection = false;
			try
			{
				reconnectAttempt = await ReceiveMessagesAsync(reconnectAttempt, cancellationToken);
				lostConnection = true;  // サーバーからのクリーンクローズ
			}
			catch (OperationCanceledException)
			{
				logger.Info("ReceiveLoopAsync: Cancelled");
				RaiseConnectionClosed();
				return;
			}
			catch (Exception ex)
			{
				logger.Error(ex, "ReceiveLoopAsync: WebSocket exception");
				lostConnection = true;
			}

			if (!lostConnection) continue;

			// 接続が失われた場合: ConnectionClosed を発火してから再接続を試みる
			logger.Info("ReceiveLoopAsync: Connection lost, raising ConnectionClosed");
			RaiseConnectionClosed();

			if (_isDisconnecting || cancellationToken.IsCancellationRequested)
			{
				logger.Info("ReceiveLoopAsync: Client-initiated disconnect, not reconnecting");
				return;
			}

			logger.Info("ReceiveLoopAsync: Attempting to reconnect...");
			int result = await AttemptReconnectAsync(reconnectAttempt, cancellationToken);
			if (result < 0)
			{
				logger.Warn("ReceiveLoopAsync: Failed to reconnect after {0} attempts", _reconnectAttemptMax);
				RaiseConnectionFailed();
				return;
			}

			reconnectAttempt = result;
			logger.Info("ReceiveLoopAsync: Reconnected successfully, continuing receive loop");
		}

		RaiseConnectionClosed();
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
			else if (messageType == MESSAGE_TYPE_SERVER_INFO)
			{
				ProcessServerInfoMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_DIAGRAM_INFO)
			{
				ProcessDiagramInfoMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_SELECT_TRAIN)
			{
				ProcessSelectTrainMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_OPERATION_COMMAND)
			{
				ProcessOperationCommandMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_HEADER_COLOR)
			{
				ProcessHeaderColorMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_NOTIFICATION)
			{
				ProcessNotificationMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_TIME_FORMAT)
			{
				ProcessTimeFormatMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_NAVIGATE_TO_HOME)
			{
				RaiseNavigateToHomeRequested();
			}
			else if (messageType == MESSAGE_TYPE_OPEN_TIMETABLE)
			{
				ProcessOpenTimetableMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_SEARCH_TRAIN_RESPONSE)
			{
				ProcessSearchTrainResponseMessage(root);
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

		double? latitude_deg = TryReadOptionalDouble(root, LATITUDE_DEG_JSON_KEY);
		double? longitude_deg = TryReadOptionalDouble(root, LONGITUDE_DEG_JSON_KEY);
		double? accuracy_m = TryReadOptionalDouble(root, ACCURACY_M_JSON_KEY);

		SyncedData syncedData = new SyncedData(
			location_m, time_ms, canStart,
			Latitude_deg: latitude_deg,
			Longitude_deg: longitude_deg,
			Accuracy_m: accuracy_m
		);
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

		// スコープを最も詳細なIDで判定 (Train > Work > WorkGroup > All)
		if (timetableData.TrainId is not null)
		{
			timetableData.Scope = TimetableScopeType.Train;
		}
		else if (timetableData.WorkId is not null)
		{
			timetableData.Scope = TimetableScopeType.Work;
		}
		else if (timetableData.WorkGroupId is not null)
		{
			timetableData.Scope = TimetableScopeType.WorkGroup;
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

	private void ProcessServerInfoMessage(JsonElement root)
	{
		var info = new ServerInfo();
		if (root.TryGetProperty(SERVER_NAME_JSON_KEY, out var n) && n.ValueKind != JsonValueKind.Null)
			info.Name = n.GetString();
		if (root.TryGetProperty(SERVER_ADMIN_JSON_KEY, out var a) && a.ValueKind != JsonValueKind.Null)
			info.Admin = a.GetString();
		if (root.TryGetProperty(SERVER_VERSION_JSON_KEY, out var v) && v.ValueKind != JsonValueKind.Null)
			info.Version = v.GetString();
		if (root.TryGetProperty(SERVER_PROTOCOL_VERSION_JSON_KEY, out var pv) && pv.ValueKind != JsonValueKind.Null)
			info.ProtocolVersion = pv.GetString();
		if (root.TryGetProperty(SERVER_FEATURES_JSON_KEY, out var features) && features.ValueKind == JsonValueKind.Array)
		{
			var list = new List<string>();
			foreach (var elem in features.EnumerateArray())
			{
				if (elem.ValueKind == JsonValueKind.String)
				{
					var s = elem.GetString();
					if (!string.IsNullOrEmpty(s)) list.Add(s);
				}
			}
			info.Features = [.. list];
			// 機能ネゴシエーション結果を公開する (IsFeatureSupported / 検索タブの表示に使用)
			ServerFeatures = list;
		}

		RaiseServerInfoUpdated(info);
	}

	private void ProcessDiagramInfoMessage(JsonElement root)
	{
		var info = new DiagramInfo();
		if (root.TryGetProperty(DIAGRAM_ID_JSON_KEY, out var id) && id.ValueKind != JsonValueKind.Null)
			info.Id = id.GetString();
		if (root.TryGetProperty(DIAGRAM_NAME_JSON_KEY, out var n) && n.ValueKind != JsonValueKind.Null)
			info.Name = n.GetString();
		if (root.TryGetProperty(DIAGRAM_DESCRIPTION_JSON_KEY, out var d) && d.ValueKind != JsonValueKind.Null)
			info.Description = d.GetString();
		if (root.TryGetProperty(DIAGRAM_WORK_GROUP_IDS_JSON_KEY, out var wgIds) && wgIds.ValueKind == JsonValueKind.Array)
		{
			var list = new List<string>();
			foreach (var elem in wgIds.EnumerateArray())
			{
				if (elem.ValueKind == JsonValueKind.String)
				{
					var s = elem.GetString();
					if (s is not null) list.Add(s);
				}
			}
			info.WorkGroupIds = [.. list];
		}

		RaiseDiagramInfoUpdated(info);
	}

	/// <summary>
	/// 列車検索の応答 (<c>SearchTrainResponse</c>) を処理する。
	/// RequestId で待機中の <see cref="SearchTrainAsync"/> を完了させる。
	/// </summary>
	private void ProcessSearchTrainResponseMessage(JsonElement root)
	{
		string? requestId = null;
		if (root.TryGetProperty(REQUEST_ID_JSON_KEY, out var reqId) && reqId.ValueKind == JsonValueKind.String)
			requestId = reqId.GetString();

		if (string.IsNullOrEmpty(requestId))
		{
			logger.Warn("ProcessSearchTrainResponseMessage: missing RequestId, ignoring");
			return;
		}

		var results = new List<TrainSearchResult>();
		if (root.TryGetProperty(SEARCH_RESULTS_JSON_KEY, out var arr) && arr.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in arr.EnumerateArray())
			{
				if (item.ValueKind != JsonValueKind.Object)
					continue;
				results.Add(new TrainSearchResult(
					WorkGroupId: ReadOptionalString(item, WORK_GROUP_ID_JSON_KEY),
					WorkId: ReadOptionalString(item, WORK_ID_JSON_KEY),
					TrainId: ReadOptionalString(item, TRAIN_ID_JSON_KEY),
					TrainNumber: ReadOptionalString(item, TRAIN_NUMBER_JSON_KEY),
					WorkName: ReadOptionalString(item, WORK_NAME_JSON_KEY),
					Direction: ReadOptionalInt(item, DIRECTION_JSON_KEY),
					StartStationName: ReadOptionalString(item, START_STATION_NAME_JSON_KEY),
					StartTime: ReadOptionalString(item, START_TIME_JSON_KEY),
					EndStationName: ReadOptionalString(item, END_STATION_NAME_JSON_KEY),
					EndTime: ReadOptionalString(item, END_TIME_JSON_KEY)
				));
			}
		}

		if (_pendingSearches.TryRemove(requestId, out var tcs))
			tcs.TrySetResult(results);
		else
			logger.Debug("ProcessSearchTrainResponseMessage: no pending search for RequestId={0}", requestId);
	}

	private static string? ReadOptionalString(JsonElement element, string propertyName)
	{
		if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
			return prop.GetString();
		return null;
	}

	private static int? ReadOptionalInt(JsonElement element, string propertyName)
	{
		if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number
			&& prop.TryGetInt32(out int value))
			return value;
		return null;
	}

	private void ProcessSelectTrainMessage(JsonElement root)
	{
		var cmd = new SelectTrainCommand
		{
			WorkGroupId = TryGetStringProperty(root, WORK_GROUP_ID_JSON_KEY),
			WorkId = TryGetStringProperty(root, WORK_ID_JSON_KEY),
			TrainId = TryGetStringProperty(root, TRAIN_ID_JSON_KEY),
		};
		RaiseTrainSelectionRequested(cmd);
	}

	private void ProcessOpenTimetableMessage(JsonElement root)
	{
		var cmd = new OpenTimetableCommand
		{
			WorkGroupId = TryGetStringProperty(root, WORK_GROUP_ID_JSON_KEY),
			WorkId = TryGetStringProperty(root, WORK_ID_JSON_KEY),
			TrainId = TryGetStringProperty(root, TRAIN_ID_JSON_KEY),
		};
		RaiseOpenTimetableRequested(cmd);
	}

	private void ProcessOperationCommandMessage(JsonElement root)
	{
		string? action = TryGetStringProperty(root, "Action");
		if (action is null)
		{
			logger.Warn("ProcessOperationCommandMessage: Missing 'Action' field");
			return;
		}
		if (!Enum.TryParse<OperationCommandType>(action, ignoreCase: true, out var parsed))
		{
			logger.Warn("ProcessOperationCommandMessage: Unknown Action '{0}'", action);
			return;
		}
		RaiseOperationCommandReceived(new OperationCommand { Action = parsed });
	}

	private void ProcessHeaderColorMessage(JsonElement root)
	{
		var cmd = new HeaderColorCommand();
		if (root.TryGetProperty("ResetToDefault", out var rd))
		{
			cmd.ResetToDefault = rd.ValueKind == JsonValueKind.True;
		}
		if (root.TryGetProperty("Color_RGB", out var color) && color.ValueKind == JsonValueKind.Number)
		{
			if (color.TryGetInt32(out int rgb))
				cmd.Color_RGB = rgb;
		}
		RaiseHeaderColorChangeRequested(cmd);
	}

	private void ProcessNotificationMessage(JsonElement root)
	{
		var n = new NotificationData
		{
			Id = TryGetStringProperty(root, "Id"),
			OrderNumber = TryGetStringProperty(root, "OrderNumber"),
			Title = TryGetStringProperty(root, "Title"),
			Body = TryGetStringProperty(root, "Body"),
			Receiver = TryGetStringProperty(root, "Receiver"),
			Sender = TryGetStringProperty(root, "Sender"),
			IconText = TryGetStringProperty(root, "IconText"),
			IconImageBase64 = TryGetStringProperty(root, "IconImageBase64"),
			SectionStartStation = TryGetStringProperty(root, "SectionStartStation"),
			SectionEndStation = TryGetStringProperty(root, "SectionEndStation"),
		};
		if (root.TryGetProperty("Priority", out var p) && p.ValueKind == JsonValueKind.Number
			&& p.TryGetInt32(out int prio))
			n.Priority = prio;
		// StationsBefore は省略/不正値のときモデル既定値 (1) を維持する。
		if (root.TryGetProperty("StationsBefore", out var sb) && sb.ValueKind == JsonValueKind.Number
			&& sb.TryGetInt32(out int stationsBefore))
			n.StationsBefore = stationsBefore;
		if (root.TryGetProperty("IconColor_RGB", out var ic))
		{
			// 数値 (0xRRGGBB の 10 進表記) と "#RRGGBB" 形式の文字列の両方を受け付ける。
			if (ic.ValueKind == JsonValueKind.Number && ic.TryGetInt32(out int iconRgb))
				n.IconColor_RGB = iconRgb;
			else if (ic.ValueKind == JsonValueKind.String && NotificationData.TryParseIconColor(ic.GetString(), out int hexIconRgb))
				n.IconColor_RGB = hexIconRgb;
		}
		if (root.TryGetProperty("IssuedAt", out var t) && t.ValueKind == JsonValueKind.String)
		{
			string? s = t.GetString();
			if (s is not null && TryParseIssuedAt(s, out var dto, out bool isUnspecifiedTimeZone))
			{
				n.IssuedAt = dto;
				n.IssuedAtIsUnspecifiedTimeZone = isUnspecifiedTimeZone;
			}
		}
		// Acknowledged は JSON の true のときのみ受領済み扱い (それ以外/欠落は false)。
		if (root.TryGetProperty("Acknowledged", out var ack))
			n.Acknowledged = ack.ValueKind == JsonValueKind.True;
		// CompactDisplay も同様に true のときのみ有効 (それ以外/欠落は false)。
		if (root.TryGetProperty("CompactDisplay", out var cd))
			n.CompactDisplay = cd.ValueKind == JsonValueKind.True;
		RaiseNotificationReceived(n);
	}

	/// <summary>
	/// <c>Notification.IssuedAt</c> の ISO 8601 文字列をパースする。オフセット
	/// (<c>Z</c> または <c>+HH:mm</c>/<c>-HH:mm</c>) を含む文字列は TZ 指定ありと判断し、
	/// 表示側で端末の現在 TZ に変換する (<see cref="DateTimeOffset.LocalDateTime"/>) ことを
	/// 前提に <paramref name="value"/> をそのまま返す。オフセットを含まない文字列は
	/// 「その時刻をそのまま表示する」ため、日時部分だけを Offset=0 の
	/// <see cref="DateTimeOffset"/> に詰めて返し (<see cref="DateTimeOffset.DateTime"/> が
	/// 元の文字列の値と一致する)、<paramref name="isUnspecifiedTimeZone"/> を true にする。
	/// <para>
	/// 日付のみ (例 <c>2024-03-01</c>) や空白区切り (例 <c>2024-03-01 09:00:00</c>) など、
	/// ISO 8601 の日時区切り <c>T</c> を含まない文字列は常に TZ 指定無し扱いとする
	/// (日付部分の <c>-</c> をオフセット記号と誤認しないため)。
	/// </para>
	/// </summary>
	private static bool TryParseIssuedAt(string s, out DateTimeOffset value, out bool isUnspecifiedTimeZone)
	{
		value = default;
		isUnspecifiedTimeZone = false;

		// 'T' (ISO 8601 の日時区切り) が無い文字列は ISO 8601 形式ではないため、
		// 日付部分の '-' をオフセット記号と誤認しないよう常に TZ 指定無し扱いにする。
		int tIndex = s.IndexOf('T');
		bool hasOffset = tIndex >= 0
			&& (s[tIndex..].Contains('Z') || s[tIndex..].Contains('+') || s[tIndex..].Contains('-'));

		if (hasOffset)
		{
			return DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.RoundtripKind, out value);
		}

		if (!DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.NoCurrentDateDefault, out var dt))
			return false;

		// オフセットは表示側で使わない (isUnspecifiedTimeZone=true のとき DateTime プロパティを
		// そのまま表示する) ため、TimeSpan.Zero を仮に詰めるだけでよい。
		value = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), TimeSpan.Zero);
		isUnspecifiedTimeZone = true;
		return true;
	}

	private void ProcessTimeFormatMessage(JsonElement root)
	{
		var cmd = new TimeFormatCommand
		{
			Format = TryGetStringProperty(root, "Format"),
		};
		RaiseTimeFormatChangeRequested(cmd);
	}

	private static string? TryGetStringProperty(JsonElement root, string name)
	{
		if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
			return prop.GetString();
		return null;
	}

	private static double? TryReadOptionalDouble(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
			return null;
		if (prop.ValueKind != JsonValueKind.Number)
			return null;
		try
		{
			return prop.GetDouble();
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private void CacheTimetableData(TimetableData timetableData)
	{
		try
		{
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
						if (jsonModels is not null)
							CacheWorkGroupSubtree(timetableData.WorkGroupId, jsonModels);
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
						if (jsonModels is not null)
							CacheWorkSubtree(timetableData.WorkGroupId, timetableData.WorkId, jsonModels);
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
						// Train スコープ単独更新の JSON には親 Work の情報 (Name / AffectDate) が含まれない。
						// IO.Models.TrainData.WorkName / AffectDate は親から引き継ぐべき値なので、
						// 既にキャッシュ済みの Work を WorkId で引いて、その Name / AffectDate を流用する。
						// (Work がまだキャッシュされていない場合は null のまま; AffectDateFormatter は
						//  「今日−DayCount」にフォールバックする既存挙動になる)
						string? inheritedWorkName = null;
						DateOnly? inheritedAffectDate = null;
						if (timetableData.WorkId is not null)
						{
							var cachedWork = FindCachedWork(timetableData.WorkId);
							inheritedWorkName = cachedWork?.Name;
							inheritedAffectDate = cachedWork?.AffectDate;
						}

						var trainData = JsonModelsConverter.ConvertTrain(jsonModels!, inheritedWorkName, inheritedAffectDate);
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
			CacheWorkGroupSubtree(workGroup.Id, workGroupData);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "CacheConvertedWorkGroup: Failed to process WorkGroup");
		}
	}

	/// <summary>
	/// WorkGroup スコープ更新を受信したときに、当該 WorkGroup 配下の
	/// Works / Trains キャッシュをペイロードに合わせて完全に再構築する。
	/// 他の WorkGroup の分は触らない。
	/// </summary>
	private void CacheWorkGroupSubtree(string workGroupId, JsonModels.WorkGroupData workGroupData)
	{
		var workGroup = JsonModelsConverter.ConvertWorkGroup(workGroupData);
		_WorkGroupCache[workGroupId] = workGroup;
		logger.Debug("CacheWorkGroupSubtree: Updated WorkGroup {0} ({1})", workGroupId, workGroup.Name);

		// 当該 WorkGroup 配下の Works / Trains キャッシュをまるごと作り直す
		// (他の WorkGroup の分は残す)
		if (_WorkListCache.TryGetValue(workGroupId, out var existingWorks))
		{
			foreach (var w in existingWorks)
				PurgeWorkSubtree(w.Id);
		}
		_WorkListCache[workGroupId] = [];

		if (workGroupData.Works is not null && workGroupData.Works.Length > 0)
		{
			foreach (var workData in workGroupData.Works)
			{
				try
				{
					var work = JsonModelsConverter.ConvertWork(workData, workGroupId);
					_WorkListCache[workGroupId].Add(work);
					RebuildTrainCacheForWork(work.Id, workData.Trains, workData);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "CacheWorkGroupSubtree: Failed to process Work");
				}
			}
		}
	}

	/// <summary>
	/// Work スコープ更新を受信したときに、当該 Work と配下の Trains キャッシュを
	/// ペイロードに合わせて完全に再構築する。他の Work の分は触らない。
	/// </summary>
	private void CacheWorkSubtree(string workGroupId, string workId, JsonModels.WorkData workData)
	{
		var work = JsonModelsConverter.ConvertWork(workData, workGroupId);

		if (!_WorkListCache.ContainsKey(workGroupId))
			_WorkListCache[workGroupId] = [];

		_WorkListCache[workGroupId].RemoveAll(w => w.Id == workId);
		_WorkListCache[workGroupId].Add(work);
		logger.Debug("CacheWorkSubtree: Updated Work {0} ({1}) under WorkGroup {2}", work.Id, work.Name, workGroupId);

		RebuildTrainCacheForWork(work.Id, workData.Trains, workData);
	}

	/// <summary>
	/// 指定の Work 配下の Train キャッシュ (TrainListByWorkIdCache / TrainDataCache) を、
	/// 渡された Trains 配列で完全に置き換える。
	/// </summary>
	/// <remarks>
	/// 前提: TrainId はちょうど一つの Work に属する (LoaderJson の WorkIdByTrainId と同じ不変条件)。
	/// この前提が崩れると、他の Work から参照されている TrainId を誤って _TrainDataCache から削除しうる。
	/// <para>
	/// <paramref name="parentWork"/> は親 WorkData。各 Train の WorkName / AffectDate は
	/// JsonModels.TrainData が持たないため、ここで親から引き継いで埋める
	/// (LoaderJson と同じ挙動。AffectDateFormatter のフォールバックを抑止する)。
	/// </para>
	/// </remarks>
	private void RebuildTrainCacheForWork(string workId, JsonModels.TrainData[]? trains, JsonModels.WorkData? parentWork)
	{
		// 古い Train を _TrainDataCache から取り除く (上記不変条件により他 Work からは参照されない)
		if (_TrainListByWorkIdCache.TryGetValue(workId, out var oldTrains))
		{
			foreach (var oldTrain in oldTrains)
				_TrainDataCache.Remove(oldTrain.Id);
		}
		_TrainListByWorkIdCache[workId] = [];

		if (trains is null || trains.Length == 0)
			return;

		foreach (var trainJson in trains)
		{
			try
			{
				var trainData = JsonModelsConverter.ConvertTrain(trainJson, parentWork);
				_TrainDataCache[trainData.Id] = trainData;
				_TrainListByWorkIdCache[workId].Add(trainData);
				logger.Debug("RebuildTrainCacheForWork: Added Train {0} ({1})", trainData.Id, trainData.TrainNumber);
			}
			catch (Exception ex)
			{
				logger.Error(ex, "RebuildTrainCacheForWork: Failed to process Train");
			}
		}
	}

	/// <summary>
	/// _WorkListCache を WorkId で線形検索する (どの WorkGroup 配下にあるかを意識せずに引きたい場合)。
	/// Train スコープ単独更新で、親 Work の Name / AffectDate を引き継ぐために使う。
	/// </summary>
	private Work? FindCachedWork(string workId)
	{
		foreach (var workList in _WorkListCache.Values)
		{
			foreach (var work in workList)
			{
				if (work.Id == workId)
					return work;
			}
		}
		return null;
	}

	/// <summary>
	/// 指定の Work 配下の Train キャッシュエントリを完全に削除する。
	/// WorkGroup 配下の構造が変化したときの掃除に使う。
	/// </summary>
	private void PurgeWorkSubtree(string workId)
	{
		if (_TrainListByWorkIdCache.TryGetValue(workId, out var oldTrains))
		{
			foreach (var oldTrain in oldTrains)
				_TrainDataCache.Remove(oldTrain.Id);
			_TrainListByWorkIdCache.Remove(workId);
		}
	}

	protected override bool IsCurrentTrainStillTracked()
	{
		// RaiseTimetableUpdated は CacheTimetableData の後に呼ばれるため、
		// All スコープ受信時はここでチェックする時点で既に新ペイロードを反映した
		// _TrainDataCache になっている。残っていれば「同じ列車が新時刻表にも存在する」
		// と見なし、駅 index / 走行フラグを維持する。
		if (string.IsNullOrEmpty(TrainId))
			return false;
		return _TrainDataCache.ContainsKey(TrainId);
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

	public override Task RequestServerInfoAsync(CancellationToken cancellationToken = default)
		=> SendRequestMessageAsync(MESSAGE_TYPE_REQUEST_SERVER_INFO, additional: null, cancellationToken);

	public override Task RequestDiagramInfoAsync(string? diagramId = null, CancellationToken cancellationToken = default)
	{
		Dictionary<string, string?>? additional = null;
		if (diagramId is not null)
			additional = new Dictionary<string, string?> { [DIAGRAM_ID_JSON_KEY] = diagramId };
		return SendRequestMessageAsync(MESSAGE_TYPE_REQUEST_DIAGRAM_INFO, additional, cancellationToken);
	}

	/// <summary>
	/// <paramref name="matchMode"/> に従って <paramref name="candidateTrainNumber"/> が
	/// <paramref name="query"/> に一致するかを判定する (UI_TEST の缶詰データ用。実サーバーでの
	/// 一致判定は <c>ReferenceNetworkSyncServer</c> 側の同名ロジックを参照)。
	/// </summary>
	internal static bool MatchesTrainNumber(string? candidateTrainNumber, string query, TrainSearchMatchMode matchMode)
	{
		if (candidateTrainNumber is null)
			return false;
		return matchMode switch
		{
			TrainSearchMatchMode.Contains => candidateTrainNumber.Contains(query, StringComparison.OrdinalIgnoreCase),
			TrainSearchMatchMode.Exact => string.Equals(candidateTrainNumber, query, StringComparison.OrdinalIgnoreCase),
			_ => candidateTrainNumber.StartsWith(query, StringComparison.OrdinalIgnoreCase),
		};
	}

	/// <summary>
	/// 列番でサーバーに列車を検索する (<c>SearchTrain</c>)。RequestId で応答を相関させ、
	/// <see cref="SearchTrainTimeoutMs"/> 以内に応答が無ければ <see cref="TimeoutException"/>。
	/// </summary>
	public override async Task<IReadOnlyList<TrainSearchResult>> SearchTrainAsync(
		string trainNumber, TrainSearchMatchMode matchMode = TrainSearchMatchMode.Prefix, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(trainNumber);
#if UI_TEST
		if (_uiTestSearchEnabled)
			return _uiTestSearchResults
				.Where(r => MatchesTrainNumber(r.TrainNumber, trainNumber, matchMode))
				.ToList();
#endif
		if (_WebSocket.State != WebSocketState.Open)
			throw new InvalidOperationException("SearchTrainAsync: WebSocket is not connected.");

		string requestId = Guid.NewGuid().ToString("N");
		var tcs = new TaskCompletionSource<IReadOnlyList<TrainSearchResult>>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		_pendingSearches[requestId] = tcs;

		try
		{
			var additional = new Dictionary<string, string?>
			{
				[REQUEST_ID_JSON_KEY] = requestId,
				[TRAIN_NUMBER_JSON_KEY] = trainNumber,
				[MATCH_MODE_JSON_KEY] = matchMode.ToString(),
			};
			await SendRequestMessageAsync(MESSAGE_TYPE_SEARCH_TRAIN, additional, cancellationToken);
			return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(SearchTrainTimeoutMs), cancellationToken);
		}
		catch (TimeoutException)
		{
			logger.Warn("SearchTrainAsync: timed out waiting for response (trainNumber={0})", trainNumber);
			throw;
		}
		finally
		{
			_pendingSearches.TryRemove(requestId, out _);
		}
	}

	/// <summary>
	/// 検索候補の完全な時刻表を取得する (<c>RequestTrainTimetable</c>)。サーバーは
	/// <c>Timetable</c> (Train スコープ) を返し、通常の受信パイプラインでキャッシュされる。
	/// 対応する TrainId の <see cref="NetworkSyncServiceBase.TimetableUpdated"/> を
	/// <see cref="FetchTrainTimetableTimeoutMs"/> 以内に待ち、キャッシュから <see cref="TrainData"/> を返す。
	/// </summary>
	public override async Task<TrainData?> FetchSearchedTrainTimetableAsync(
		TrainSearchResult result, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(result);
		if (string.IsNullOrEmpty(result.TrainId))
			throw new ArgumentException("TrainSearchResult.TrainId must not be empty.", nameof(result));
#if UI_TEST
		if (_uiTestSearchEnabled)
		{
			_uiTestSearchTrainData.TryGetValue(result.TrainId, out var cannedTrain);
			if (cannedTrain is not null)
				_TrainDataCache[cannedTrain.Id] = cannedTrain;
			return cannedTrain;
		}
#endif
		if (_WebSocket.State != WebSocketState.Open)
			throw new InvalidOperationException("FetchSearchedTrainTimetableAsync: WebSocket is not connected.");

		string trainId = result.TrainId;

		// 既にキャッシュ済みなら再取得せず即返す。
		var cached = GetTrainData(trainId);
		if (cached is not null)
			return cached;

		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		void OnTimetable(object? sender, TimetableData data)
		{
			if (string.Equals(data.TrainId, trainId, StringComparison.Ordinal))
				tcs.TrySetResult(true);
		}

		TimetableUpdated += OnTimetable;
		try
		{
			var additional = new Dictionary<string, string?>
			{
				[REQUEST_ID_JSON_KEY] = Guid.NewGuid().ToString("N"),
				[WORK_GROUP_ID_JSON_KEY] = result.WorkGroupId,
				[WORK_ID_JSON_KEY] = result.WorkId,
				[TRAIN_ID_JSON_KEY] = trainId,
			};
			await SendRequestMessageAsync(MESSAGE_TYPE_REQUEST_TRAIN_TIMETABLE, additional, cancellationToken);
			await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(FetchTrainTimetableTimeoutMs), cancellationToken);
		}
		catch (TimeoutException)
		{
			logger.Warn("FetchSearchedTrainTimetableAsync: timed out (trainId={0})", trainId);
			throw;
		}
		finally
		{
			TimetableUpdated -= OnTimetable;
		}

		return GetTrainData(trainId);
	}

	/// <summary>
	/// 通告の受領 (<c>AcknowledgeNotification</c>) をサーバーへ送信する。
	/// 送信は SendRequestMessageAsync 経由でセマフォ直列化され、ソケットが未接続の
	/// 場合はベストエフォートで無視される (呼び出し側はローカルの既読状態を維持する)。
	/// </summary>
	public override Task AcknowledgeNotificationAsync(string id, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		var additional = new Dictionary<string, string?> { ["Id"] = id };
		return SendRequestMessageAsync(MESSAGE_TYPE_ACKNOWLEDGE_NOTIFICATION, additional, cancellationToken);
	}

	private async Task SendRequestMessageAsync(
		string messageType,
		Dictionary<string, string?>? additional,
		CancellationToken cancellationToken)
	{
		if (_WebSocket.State != WebSocketState.Open)
		{
			logger.Warn("SendRequestMessageAsync ({0}): WebSocket is not open", messageType);
			return;
		}

		try
		{
			var payload = new Dictionary<string, string?> { [MESSAGE_TYPE_JSON_KEY] = messageType };
			if (additional is not null)
			{
				foreach (var kv in additional)
					payload[kv.Key] = kv.Value;
			}
			string json = JsonSerializer.Serialize(payload);
			logger.Debug("SendRequestMessageAsync: Sending request: {0}", json);
			byte[] bytes = Encoding.UTF8.GetBytes(json);
			await _sendLock.WaitAsync(cancellationToken);
			try
			{
				await _WebSocket.SendAsync(
					new ArraySegment<byte>(bytes),
					WebSocketMessageType.Text,
					endOfMessage: true,
					cancellationToken
				);
			}
			finally
			{
				_sendLock.Release();
			}
		}
		catch (WebSocketException ex)
		{
			logger.Error(ex, "SendRequestMessageAsync ({0}): WebSocket exception", messageType);
		}
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
			await _sendLock.WaitAsync();
			try
			{
				await _WebSocket.SendAsync(
					new ArraySegment<byte>(bytes),
					WebSocketMessageType.Text,
					endOfMessage: true,
					CancellationToken.None
				);
			}
			finally
			{
				_sendLock.Release();
			}
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

#if UI_TEST
	/// <summary>
	/// UI_TEST 専用: 実サーバー無しで、この WebSocket ローダーの ILoader キャッシュを
	/// 別ローダー (サンプルデータ等) の内容で埋める。AppBar の接続ステータス表示 (#266)
	/// を DTAC 画面で検証するために、データを持つ "WebSocket 型" のローダーを
	/// ネットワーク無しで用意する用途。
	/// </summary>
	public void SeedCachesFromLoaderForTesting(TRViS.IO.ILoader source)
	{
		ArgumentNullException.ThrowIfNull(source);
		foreach (var wg in source.GetWorkGroupList())
		{
			_WorkGroupCache[wg.Id] = wg;
			var works = source.GetWorkList(wg.Id).ToList();
			_WorkListCache[wg.Id] = works;
			foreach (var w in works)
			{
				var trains = source.GetTrainDataList(w.Id).ToList();
				_TrainListByWorkIdCache[w.Id] = trains;
				foreach (var t in trains)
					_TrainDataCache[t.Id] = t;
			}
		}
	}

	private bool _uiTestSearchEnabled;
	private readonly List<TrainSearchResult> _uiTestSearchResults = new();
	private readonly Dictionary<string, TrainData> _uiTestSearchTrainData = new();

	/// <summary>
	/// UI_TEST 専用: 実サーバー無しで列車検索を成立させる。<c>TrainSearch</c> 機能を
	/// 有効化し、<see cref="SearchTrainAsync"/> / <see cref="FetchSearchedTrainTimetableAsync"/>
	/// が渡された canned データを返すようにする。
	/// </summary>
	public void SeedTrainSearchForTesting(IEnumerable<(TrainSearchResult Summary, TrainData Data)> trains)
	{
		ArgumentNullException.ThrowIfNull(trains);
		_uiTestSearchEnabled = true;
		ServerFeatures = new[] { ServerFeatureIds.TrainSearch };
		_uiTestSearchResults.Clear();
		_uiTestSearchTrainData.Clear();
		foreach (var (summary, data) in trains)
		{
			_uiTestSearchResults.Add(summary);
			if (summary.TrainId is not null)
				_uiTestSearchTrainData[summary.TrainId] = data;
		}
	}
#endif

	private async Task<int> AttemptReconnectAsync(int reconnectAttempt, CancellationToken cancellationToken)
	{
		logger.Info("AttemptReconnectAsync: Starting reconnection attempts (max: {0})", _reconnectAttemptMax);
		RaiseReconnecting();

		while (reconnectAttempt < _reconnectAttemptMax && !cancellationToken.IsCancellationRequested)
		{
			reconnectAttempt++;
			logger.Info("AttemptReconnectAsync: Attempt {0}/{1}", reconnectAttempt, _reconnectAttemptMax);

			try
			{
				// 再接続間隔を待つ
				await Task.Delay(_reconnectIntervalMs, cancellationToken);

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

				// 再接続後にIDを再送信し、サーバーが正しいスコープで配信できるようにする
				await SendIdUpdateAsync();

				logger.Info("AttemptReconnectAsync: Successfully reconnected on attempt {0}", reconnectAttempt);
				RaiseReconnected();
				return reconnectAttempt;  // 再接続成功 (ReceiveLoopAsync がループを再開する)
			}
			catch (OperationCanceledException)
			{
				logger.Info("AttemptReconnectAsync: Cancelled");
				return -1;
			}
			catch (WebSocketException ex)
			{
				logger.Warn(ex, "AttemptReconnectAsync: Reconnection attempt {0} failed", reconnectAttempt);
				if (reconnectAttempt < _reconnectAttemptMax)
				{
					logger.Info("AttemptReconnectAsync: Retrying in {0}ms", _reconnectIntervalMs);
				}
			}
			catch (Exception ex)
			{
				logger.Error(ex, "AttemptReconnectAsync: Unexpected exception during reconnection attempt {0}", reconnectAttempt);
				if (reconnectAttempt < _reconnectAttemptMax)
				{
					logger.Info("AttemptReconnectAsync: Retrying in {0}ms", _reconnectIntervalMs);
				}
			}
		}

		logger.Warn("AttemptReconnectAsync: All reconnection attempts failed");
		return -1;  // 再接続失敗
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken = default)
	{
		logger.Info("DisconnectAsync: Disconnecting");
		_isDisconnecting = true;
		_ReceiveLoopCts?.Cancel();

		// 受信ループのキャンセルにより ClientWebSocket が内部で Abort/Dispose 済みの場合、
		// CloseAsync は ObjectDisposedException や InvalidOperationException を投げ得る。
		// シャットダウンパスではいずれも握りつぶして良い。
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
			catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or InvalidOperationException)
			{
				logger.Warn(ex, "DisconnectAsync: WebSocket already closed or disposed");
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
		_isDisconnecting = true;
		_ReceiveLoopCts?.Cancel();
		_ReceiveLoopCts?.Dispose();
		_WebSocket.Dispose();
		_sendLock.Dispose();
	}
}
