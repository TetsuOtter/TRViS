using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;

using TR.SimpleHttpServer;
using TR.SimpleHttpServer.WebSocket;

namespace TRViS.ReferenceServer;

/// <summary>
/// HTTP + WebSocket 両対応のリファレンスサーバー。
/// 実プロトコルに準拠した動作を提供し、Control API 経由でテストから状態を制御できる。
/// </summary>
public sealed class ReferenceNetworkSyncServer : IDisposable
{
	private readonly HttpServer _httpServer;

	// --- サーバー状態 (immutable record + lock で一貫性を保証) ---
	private sealed record ServerState(long Time_ms, double Location_m, bool CanStart);
	private readonly object _stateLock = new();
	private ServerState _state = new(0, double.NaN, false);

	// --- HTTP クエリログ ---
	private readonly ConcurrentQueue<ReceivedHttpQueryDto> _httpQueryLog = new();

	// --- WebSocket クライアント管理 ---
	private readonly ConcurrentDictionary<string, ClientState> _wsClients = new();

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = false,
	};

	public ushort Port => _httpServer.Port;

	public ReferenceNetworkSyncServer(ushort port = 0)
	{
		_httpServer = new HttpServer(port, HandleHttpAsync, SelectWsHandlerAsync);
	}

	public void Start() => _httpServer.Start();
	public void Stop() => _httpServer.Stop();

	public void Dispose() => _httpServer.Dispose();

	// ================================================================
	// HTTP ルーティング
	// ================================================================

	private async Task<HttpResponse> HandleHttpAsync(HttpRequest request)
	{
		if (request.Path.StartsWith("/control", StringComparison.OrdinalIgnoreCase))
			return await HandleControlApiAsync(request);

		if (request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
			return OkJson("{\"ok\":true}");

		return HandleSyncEndpoint(request);
	}

	/// <summary>
	/// HTTP 同期エンドポイント: SyncedData JSON を返す。
	/// クライアントが送ったクエリパラメータ (workgroup/work/train) を記録する。
	/// </summary>
	private HttpResponse HandleSyncEndpoint(HttpRequest request)
	{
		_httpQueryLog.Enqueue(new ReceivedHttpQueryDto(
			WorkGroupId: request.QueryString["workgroup"],
			WorkId: request.QueryString["work"],
			TrainId: request.QueryString["train"],
			ReceivedAt: DateTime.UtcNow
		));

		var state = _state;
		double? locationForJson = double.IsNaN(state.Location_m) ? null : state.Location_m;
		string json = JsonSerializer.Serialize(new
		{
			Location_m = locationForJson,
			Time_ms = state.Time_ms,
			CanStart = state.CanStart,
		});
		return OkJson(json);
	}

	// ================================================================
	// Control API
	// ================================================================

	private async Task<HttpResponse> HandleControlApiAsync(HttpRequest request)
	{
		string method = request.Method.ToUpperInvariant();
		string sub = request.Path.Length > "/control".Length
			? request.Path["/control".Length..].TrimStart('/')
			: string.Empty;

		return (method, sub.ToLowerInvariant()) switch
		{
			("GET", "state") => GetState(),
			("POST", "state") => SetState(request),
			("POST", "broadcast-synced") => await BroadcastSyncedAsync(),
			("POST", "broadcast-timetable") => await BroadcastTimetableAsync(request),
			("GET", "http-queries") => GetHttpQueries(),
			("DELETE", "http-queries") => ClearHttpQueries(),
			("GET", "ws-clients") => GetWsClients(),
			("POST", "disconnect-all") => await DisconnectAllAsync(),
			("POST", "reset") => Reset(),
			_ => Error(HttpStatusCode.NotFound, $"Unknown control endpoint: {method} /control/{sub}"),
		};
	}

	private HttpResponse GetState()
	{
		var state = _state;
		double? locationForJson = double.IsNaN(state.Location_m) ? null : state.Location_m;
		return OkJson(JsonSerializer.Serialize(new
		{
			Time_ms = state.Time_ms,
			Location_m = locationForJson,
			CanStart = state.CanStart,
		}));
	}

	private HttpResponse SetState(HttpRequest request)
	{
		try
		{
			string body = Encoding.UTF8.GetString(request.Body);
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;

			lock (_stateLock)
			{
				var (time, location, canStart) = (_state.Time_ms, _state.Location_m, _state.CanStart);

				if (root.TryGetProperty("Time_ms", out var t) && t.ValueKind != JsonValueKind.Null)
					time = t.GetInt64();
				if (root.TryGetProperty("Location_m", out var l))
					location = l.ValueKind == JsonValueKind.Null ? double.NaN : l.GetDouble();
				if (root.TryGetProperty("CanStart", out var cs) && cs.ValueKind != JsonValueKind.Null)
					canStart = cs.GetBoolean();

				_state = new ServerState(time, location, canStart);
			}

			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex)
		{
			return Error(HttpStatusCode.BadRequest, ex.Message);
		}
	}

	private async Task<HttpResponse> BroadcastSyncedAsync()
	{
		var state = _state;
		double? locationForJson = double.IsNaN(state.Location_m) ? null : state.Location_m;
		string json = JsonSerializer.Serialize(new
		{
			MessageType = "SyncedData",
			Location_m = locationForJson,
			Time_ms = state.Time_ms,
			CanStart = state.CanStart,
		});
		await BroadcastTextAsync(json);
		return OkJson("{\"ok\":true}");
	}

	/// <summary>
	/// 時刻表データを全 WebSocket クライアントに配信する。
	/// リクエストボディ: { WorkGroupId?, WorkId?, TrainId?, Data: string (JSON) }
	/// Data は JSON 文字列として受け取り、WS メッセージには raw JSON として埋め込む。
	/// </summary>
	private async Task<HttpResponse> BroadcastTimetableAsync(HttpRequest request)
	{
		try
		{
			string body = Encoding.UTF8.GetString(request.Body);
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;

			string? workGroupId = TryGetString(root, "WorkGroupId");
			string? workId = TryGetString(root, "WorkId");
			string? trainId = TryGetString(root, "TrainId");

			if (!root.TryGetProperty("Data", out var dataProp))
				return Error(HttpStatusCode.BadRequest, "Missing 'Data' field");

			// WS メッセージを構築: Data は raw JSON として埋め込む
			using var ms = new MemoryStream();
			using (var writer = new Utf8JsonWriter(ms))
			{
				writer.WriteStartObject();
				writer.WriteString("MessageType", "Timetable");
				if (workGroupId is not null) writer.WriteString("WorkGroupId", workGroupId);
				if (workId is not null) writer.WriteString("WorkId", workId);
				if (trainId is not null) writer.WriteString("TrainId", trainId);
				writer.WritePropertyName("Data");

				// Data が文字列なら内部 JSON としてパースして埋め込む
				if (dataProp.ValueKind == JsonValueKind.String)
				{
					using var innerDoc = JsonDocument.Parse(dataProp.GetString()!);
					innerDoc.RootElement.WriteTo(writer);
				}
				else
				{
					dataProp.WriteTo(writer);
				}
				writer.WriteEndObject();
			}

			string message = Encoding.UTF8.GetString(ms.ToArray());
			await BroadcastTextAsync(message);
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex)
		{
			return Error(HttpStatusCode.BadRequest, ex.Message);
		}
	}

	private HttpResponse GetHttpQueries()
		=> OkJson(JsonSerializer.Serialize(_httpQueryLog.ToArray(), JsonOptions));

	private HttpResponse ClearHttpQueries()
	{
		while (_httpQueryLog.TryDequeue(out _)) { }
		return OkJson("{\"ok\":true}");
	}

	private HttpResponse GetWsClients()
	{
		var clients = _wsClients.Values.Select(c => new WsClientDto(
			c.ConnectionId, c.WorkGroupId, c.WorkId, c.TrainId
		)).ToArray();
		return OkJson(JsonSerializer.Serialize(clients, JsonOptions));
	}

	private async Task<HttpResponse> DisconnectAllAsync()
	{
		var tasks = _wsClients.Values
			.Where(c => c.Connection.IsOpen)
			.Select(c => TryCloseAsync(c.Connection))
			.ToList();
		await Task.WhenAll(tasks);
		return OkJson("{\"ok\":true}");
	}

	private HttpResponse Reset()
	{
		_state = new ServerState(0, double.NaN, false);
		while (_httpQueryLog.TryDequeue(out _)) { }
		return OkJson("{\"ok\":true}");
	}

	// ================================================================
	// WebSocket ハンドリング
	// ================================================================

	private Task<WebSocketHandler?> SelectWsHandlerAsync(string path)
		=> Task.FromResult<WebSocketHandler?>(HandleWsConnectionAsync);

	private async Task HandleWsConnectionAsync(HttpRequest request, WebSocketConnection ws)
	{
		string id = Guid.NewGuid().ToString("N");
		var state = new ClientState(id, ws);
		_wsClients[id] = state;

		try
		{
			while (ws.IsOpen)
			{
				var msg = await ws.ReceiveMessageAsync(CancellationToken.None);
				if (msg.Type == WebSocketMessageType.Close)
				{
					if (ws.IsOpen)
						await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
					break;
				}
				if (msg.Type == WebSocketMessageType.Text)
					ProcessClientIdUpdate(state, msg.GetText());
			}
		}
		catch (Exception)
		{
			// 強制切断など
		}
		finally
		{
			_wsClients.TryRemove(id, out _);
			ws.Dispose();
		}
	}

	private static void ProcessClientIdUpdate(ClientState client, string message)
	{
		try
		{
			using var doc = JsonDocument.Parse(message);
			var root = doc.RootElement;
			if (root.TryGetProperty("WorkGroupId", out var wg)) client.WorkGroupId = wg.GetString();
			if (root.TryGetProperty("WorkId", out var w)) client.WorkId = w.GetString();
			if (root.TryGetProperty("TrainId", out var t)) client.TrainId = t.GetString();
		}
		catch (JsonException) { }
	}

	private async Task BroadcastTextAsync(string message)
	{
		var tasks = _wsClients.Values
			.Where(c => c.Connection.IsOpen)
			.Select(c => TrySendAsync(c.Connection, message))
			.ToList();
		await Task.WhenAll(tasks);
	}

	private static async Task TrySendAsync(WebSocketConnection ws, string message)
	{
		try { await ws.SendTextAsync(message, CancellationToken.None); }
		catch { /* クライアントが切断済みの場合は無視 */ }
	}

	private static async Task TryCloseAsync(WebSocketConnection ws)
	{
		try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
		catch { /* 既に切断済みの場合は無視 */ }
	}

	// ================================================================
	// ヘルパー
	// ================================================================

	private static string? TryGetString(JsonElement element, string propertyName)
	{
		if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
			return prop.GetString();
		return null;
	}

	private static HttpResponse OkJson(string json)
		=> new(HttpStatusCode.OK, "application/json", new NameValueCollection(), json);

	private static HttpResponse Error(HttpStatusCode status, string message)
		=> new(status, "text/plain", new NameValueCollection(), message);

	// ================================================================
	// 内部型
	// ================================================================

	private sealed class ClientState
	{
		public string ConnectionId { get; }
		public WebSocketConnection Connection { get; }
		public string? WorkGroupId { get; set; }
		public string? WorkId { get; set; }
		public string? TrainId { get; set; }

		public ClientState(string connectionId, WebSocketConnection connection)
		{
			ConnectionId = connectionId;
			Connection = connection;
		}
	}
}

// DTO: テストプロジェクトから共有するため public に定義
public sealed record ReceivedHttpQueryDto(
	string? WorkGroupId,
	string? WorkId,
	string? TrainId,
	DateTime ReceivedAt
);

public sealed record WsClientDto(
	string ConnectionId,
	string? WorkGroupId,
	string? WorkId,
	string? TrainId
);
