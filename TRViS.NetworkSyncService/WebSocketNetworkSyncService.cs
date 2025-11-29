using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using TRViS.IO;
using TRViS.IO.Models;

namespace TRViS.NetworkSyncService;

/// <summary>
/// WebSocket-based implementation of NetworkSyncService
/// </summary>
public class WebSocketNetworkSyncService : NetworkSyncServiceBase, ILoader
{
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

	private readonly ClientWebSocket _WebSocket;
	private readonly Uri _Uri;
	private readonly byte[] _ReceiveBuffer = new byte[4096];
	private SyncedData _LatestData = new(double.NaN, 0, false);
	private CancellationTokenSource? _ReceiveLoopCts;
	private Task? _ReceiveLoopTask;

	// Ping/Pong管理用
	private Task? _PingLoopTask;
	private CancellationTokenSource? _PingLoopCts;
	private DateTime _LastPongReceivedTime = DateTime.UtcNow;
	private readonly object _PongLock = new();
	private const int PING_INTERVAL_MS = 10000;  // 10秒
	private const int PONG_TIMEOUT_MS = 30000;   // 30秒

	// ILoader実装用のキャッシュ
	private readonly Dictionary<string, WorkGroup> _WorkGroupCache = new();
	private readonly Dictionary<string, List<Work>> _WorkListCache = new();
	private readonly Dictionary<string, TrainData> _TrainDataCache = new();
	private readonly Dictionary<string, List<TrainData>> _TrainListByWorkIdCache = new();

	public WebSocketNetworkSyncService(Uri uri, ClientWebSocket webSocket)
	{
		_Uri = uri;
		_WebSocket = webSocket;
	}

	public async Task ConnectAsync(CancellationToken cancellationToken)
	{
		if (_WebSocket.State == WebSocketState.Open)
			return;

		await _WebSocket.ConnectAsync(_Uri, cancellationToken);
		StartReceiveLoop();
	}

	private void StartReceiveLoop()
	{
		_ReceiveLoopCts = new CancellationTokenSource();
		_ReceiveLoopTask = ReceiveLoopAsync(_ReceiveLoopCts.Token);

		// Pingループを開始
		_PingLoopCts = new CancellationTokenSource();
		_PingLoopTask = PingLoopAsync(_PingLoopCts.Token);
	}

	private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
	{
		StringBuilder messageBuilder = new();

		try
		{
			while (!cancellationToken.IsCancellationRequested && _WebSocket.State == WebSocketState.Open)
			{
				WebSocketReceiveResult result = await _WebSocket.ReceiveAsync(
					new ArraySegment<byte>(_ReceiveBuffer),
					cancellationToken
				);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					await _WebSocket.CloseAsync(
						WebSocketCloseStatus.NormalClosure,
						"Closing",
						CancellationToken.None
					);
					break;
				}

				// Pongフレームを受信
				if (result.MessageType == WebSocketMessageType.Binary)
				{
					// Pongフレームの処理
					lock (_PongLock)
					{
						_LastPongReceivedTime = DateTime.UtcNow;
					}
					continue;
				}

				if (result.MessageType == WebSocketMessageType.Text)
				{
					messageBuilder.Append(Encoding.UTF8.GetString(_ReceiveBuffer, 0, result.Count));

					if (result.EndOfMessage)
					{
						string message = messageBuilder.ToString();
						messageBuilder.Clear();
						ProcessMessage(message);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
		}
		catch (WebSocketException)
		{
			// Connection closed or error occurred
		}
		finally
		{
			// 接続が切断されたことを通知
			RaiseConnectionClosed();
		}
	}

	private void ProcessMessage(string message)
	{
		try
		{
			using JsonDocument? json = JsonDocument.Parse(message);
			if (json is null)
				return;

			JsonElement root = json.RootElement;

			// メッセージタイプを確認
			string? messageType = null;
			try
			{
				messageType = root.GetProperty(MESSAGE_TYPE_JSON_KEY).GetString();
			}
			catch (KeyNotFoundException) { }

			if (messageType == MESSAGE_TYPE_SYNCED_DATA)
			{
				ProcessSyncedDataMessage(root);
			}
			else if (messageType == MESSAGE_TYPE_TIMETABLE)
			{
				ProcessTimetableMessage(root);
			}
		}
		catch (JsonException)
		{
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
				case TimetableScopeType.WorkGroup:
					if (timetableData.WorkGroupId is not null)
					{
						// WorkGroupの情報をキャッシュ
						var workGroup = new WorkGroup(
							Id: timetableData.WorkGroupId,
							Name: dataElement.TryGetProperty("Name", out var nameElem) ? nameElem.GetString() ?? "" : ""
						);
						_WorkGroupCache[timetableData.WorkGroupId] = workGroup;
					}
					break;

				case TimetableScopeType.Work:
					if (timetableData.WorkId is not null && timetableData.WorkGroupId is not null)
					{
						// Workの情報をキャッシュ
						var work = new Work(
							Id: timetableData.WorkId,
							WorkGroupId: timetableData.WorkGroupId,
							Name: dataElement.TryGetProperty("Name", out var workNameElem) ? workNameElem.GetString() ?? "" : ""
						);

						if (!_WorkListCache.ContainsKey(timetableData.WorkGroupId))
							_WorkListCache[timetableData.WorkGroupId] = new List<Work>();

						// 既存のWorkを削除して追加（更新）
						_WorkListCache[timetableData.WorkGroupId].RemoveAll(w => w.Id == timetableData.WorkId);
						_WorkListCache[timetableData.WorkGroupId].Add(work);
					}
					break;

				case TimetableScopeType.Train:
					if (timetableData.TrainId is not null)
					{
						// TrainDataの情報をキャッシュ
						var trainData = new TrainData(
							Id: timetableData.TrainId,
							Direction: Direction.Outbound
						);
						_TrainDataCache[timetableData.TrainId] = trainData;

						// WorkIdに紐づくTrainのリストにも追加
						if (timetableData.WorkId is not null)
						{
							if (!_TrainListByWorkIdCache.ContainsKey(timetableData.WorkId))
								_TrainListByWorkIdCache[timetableData.WorkId] = new List<TrainData>();

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

	protected override void OnWorkGroupIdChanged(string? value)
	{
		_ = SendIdUpdateAsync();
	}

	protected override void OnWorkIdChanged(string? value)
	{
		_ = SendIdUpdateAsync();
	}

	protected override void OnTrainIdChanged(string? value)
	{
		_ = SendIdUpdateAsync();
	}

	private async Task SendIdUpdateAsync()
	{
		if (_WebSocket.State != WebSocketState.Open)
			return;

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
			byte[] bytes = Encoding.UTF8.GetBytes(json);
			await _WebSocket.SendAsync(
				new ArraySegment<byte>(bytes),
				WebSocketMessageType.Text,
				endOfMessage: true,
				CancellationToken.None
			);
		}
		catch (WebSocketException)
		{
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

	private async Task PingLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested && _WebSocket.State == WebSocketState.Open)
			{
				// 10秒ごとにPingフレームを送信
				await Task.Delay(PING_INTERVAL_MS, cancellationToken);

				if (cancellationToken.IsCancellationRequested || _WebSocket.State != WebSocketState.Open)
					break;

				try
				{
					// Pingフレームを送信（制御フレーム）
					await _WebSocket.SendAsync(
						new ArraySegment<byte>(Array.Empty<byte>()),
						WebSocketMessageType.Binary,
						endOfMessage: true,
						cancellationToken
					);
				}
				catch (WebSocketException)
				{
					// Ping送信に失敗したので接続を切断
					await ForceDisconnectAsync();
					break;
				}
				catch (OperationCanceledException)
				{
					// キャンセルされた場合
					break;
				}

				// Pongが返ってきたか確認
				bool isPongTimeout;
				lock (_PongLock)
				{
					isPongTimeout = DateTime.UtcNow - _LastPongReceivedTime > TimeSpan.FromMilliseconds(PONG_TIMEOUT_MS);
				}

				if (isPongTimeout)
				{
					// 30秒以内にPongが返ってこなかったので接続を切断
					await ForceDisconnectAsync();
					break;
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
		}
		catch (Exception)
		{
			await ForceDisconnectAsync();
		}
	}

	private async Task ForceDisconnectAsync()
	{
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
		catch (WebSocketException)
		{
			// Already closed
		}

		// ReceiveLoopを強制終了
		_ReceiveLoopCts?.Cancel();
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken = default)
	{
		_ReceiveLoopCts?.Cancel();
		_PingLoopCts?.Cancel();

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
			catch (WebSocketException)
			{
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
				// Expected
			}
		}

		if (_PingLoopTask is not null)
		{
			try
			{
				await _PingLoopTask;
			}
			catch (OperationCanceledException)
			{
				// Expected
			}
		}
	}

	public override void Dispose()
	{
		if (_IsDisposed)
			return;

		_IsDisposed = true;
		_ReceiveLoopCts?.Cancel();
		_ReceiveLoopCts?.Dispose();
		_PingLoopCts?.Cancel();
		_PingLoopCts?.Dispose();
		_WebSocket.Dispose();
	}
}
