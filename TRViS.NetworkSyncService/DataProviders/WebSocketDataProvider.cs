using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TRViS.NetworkSyncService.DataProviders;

public class WebSocketDataProvider : IDataProvider, IDisposable
{
	// SyncedDataメッセージのJSONキー
	const string LOCATION_M_JSON_KEY = "Location_m";
	const string TIME_MS_JSON_KEY = "Time_ms";
	const string CAN_START_JSON_KEY = "CanStart";

	// ID更新メッセージのJSONキー
	const string WORK_GROUP_ID_JSON_KEY = "WorkGroupId";
	const string WORK_ID_JSON_KEY = "WorkId";
	const string TRAIN_ID_JSON_KEY = "TrainId";

	// 時刻表メッセージのJSONキー
	const string MESSAGE_TYPE_JSON_KEY = "MessageType";
	const string MESSAGE_TYPE_SYNCED_DATA = "SyncedData";
	const string MESSAGE_TYPE_TIMETABLE = "Timetable";
	const string TIMETABLE_DATA_JSON_KEY = "Data";

	private readonly ClientWebSocket _WebSocket;
	private readonly Uri _Uri;
	private readonly byte[] _ReceiveBuffer = new byte[4096];
	private SyncedData _LatestData = new(double.NaN, 0, false);
	private bool _IsDisposed;
	private CancellationTokenSource? _ReceiveLoopCts;
	private Task? _ReceiveLoopTask;

	private string? _WorkGroupId;
	public string? WorkGroupId
	{
		get => _WorkGroupId;
		set
		{
			if (_WorkGroupId == value)
				return;
			_WorkGroupId = value;
			_ = SendIdUpdateAsync();
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
			_ = SendIdUpdateAsync();
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
			_ = SendIdUpdateAsync();
		}
	}

	public event EventHandler<TimetableData>? TimetableUpdated;

	public WebSocketDataProvider(Uri uri, ClientWebSocket webSocket)
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

		_LatestData = new SyncedData(location_m, time_ms, canStart);
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
			}
		}
		catch (FormatException) { }

		// イベントを発火
		TimetableUpdated?.Invoke(this, timetableData);
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

	public Task<SyncedData> GetSyncedDataAsync(CancellationToken token)
	{
		// WebSocket is event-driven, return the latest cached data
		return Task.FromResult(_LatestData);
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken = default)
	{
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
	}

	public void Dispose()
	{
		if (_IsDisposed)
			return;

		_IsDisposed = true;
		_ReceiveLoopCts?.Cancel();
		_ReceiveLoopCts?.Dispose();
		_WebSocket.Dispose();
	}
}
