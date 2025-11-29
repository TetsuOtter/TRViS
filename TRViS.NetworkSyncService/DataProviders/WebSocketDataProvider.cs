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
	const string WORK_GROUP_ID_JSON_KEY = "WorkGroupId";
	const string WORK_ID_JSON_KEY = "WorkId";
	const string TRAIN_ID_JSON_KEY = "TrainId";

	const string LOCATION_M_JSON_KEY = "Location_m";
	const string TIME_MS_JSON_KEY = "Time_ms";
	const string CAN_START_JSON_KEY = "CanStart";

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
		catch (JsonException)
		{
			// Invalid JSON, ignore
		}
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
