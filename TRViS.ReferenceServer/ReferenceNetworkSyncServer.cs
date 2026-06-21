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
	private sealed record ServerState(
		long Time_ms,
		double Location_m,
		bool CanStart,
		double? Latitude_deg,
		double? Longitude_deg,
		double? Accuracy_m
	);
	private readonly object _stateLock = new();
	private ServerState _state = new(0, double.NaN, false, null, null, null);

	// --- HTTP クエリログ ---
	private readonly ConcurrentQueue<ReceivedHttpQueryDto> _httpQueryLog = new();

	// --- WebSocket クライアント管理 ---
	private readonly ConcurrentDictionary<string, ClientState> _wsClients = new();

	// --- サーバー情報・ダイヤ情報 ---
	private readonly object _infoLock = new();
	private ServerInfoState _serverInfo = new(
		Name: "TRViS Reference Server",
		Admin: null,
		Version: "0.0.0",
		ProtocolVersion: "1.0"
	);
	// DiagramId -> DiagramInfoState のマップ。null キーで「カレント」を表現。
	private readonly Dictionary<string, DiagramInfoState> _diagrams = new();
	private string? _currentDiagramId;

	// --- 受信した RequestServerInfo / RequestDiagramInfo のログ (テスト用) ---
	private readonly ConcurrentQueue<ReceivedRequestDto> _receivedRequests = new();

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
			Latitude_deg = state.Latitude_deg,
			Longitude_deg = state.Longitude_deg,
			Accuracy_m = state.Accuracy_m,
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
			// サーバー情報・ダイヤ情報
			("GET", "server-info") => GetServerInfo(),
			("POST", "server-info") => SetServerInfo(request),
			("POST", "broadcast-server-info") => await BroadcastServerInfoAsync(),
			("GET", "diagrams") => GetDiagrams(),
			("POST", "diagrams") => SetDiagram(request),
			("POST", "broadcast-diagram") => await BroadcastDiagramAsync(request),
			// 受信要求ログ (テスト用)
			("GET", "received-requests") => GetReceivedRequests(),
			("DELETE", "received-requests") => ClearReceivedRequests(),
			// リモートコマンド配信
			("POST", "broadcast-select-train") => await BroadcastSelectTrainAsync(request),
			("POST", "broadcast-operation-command") => await BroadcastOperationCommandAsync(request),
			("POST", "broadcast-header-color") => await BroadcastHeaderColorAsync(request),
			("POST", "broadcast-notification") => await BroadcastNotificationAsync(request),
			("POST", "broadcast-time-format") => await BroadcastTimeFormatAsync(request),
			("POST", "broadcast-navigate-to-home") => await BroadcastNavigateToHomeAsync(),
			("POST", "broadcast-open-timetable") => await BroadcastOpenTimetableAsync(request),
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
			Latitude_deg = state.Latitude_deg,
			Longitude_deg = state.Longitude_deg,
			Accuracy_m = state.Accuracy_m,
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
				double? latitude = _state.Latitude_deg;
				double? longitude = _state.Longitude_deg;
				double? accuracy = _state.Accuracy_m;

				if (root.TryGetProperty("Time_ms", out var t) && t.ValueKind != JsonValueKind.Null)
					time = t.GetInt64();
				if (root.TryGetProperty("Location_m", out var l))
					location = l.ValueKind == JsonValueKind.Null ? double.NaN : l.GetDouble();
				if (root.TryGetProperty("CanStart", out var cs) && cs.ValueKind != JsonValueKind.Null)
					canStart = cs.GetBoolean();
				if (root.TryGetProperty("Latitude_deg", out var lat))
					latitude = lat.ValueKind == JsonValueKind.Null ? null : lat.GetDouble();
				if (root.TryGetProperty("Longitude_deg", out var lon))
					longitude = lon.ValueKind == JsonValueKind.Null ? null : lon.GetDouble();
				if (root.TryGetProperty("Accuracy_m", out var acc))
					accuracy = acc.ValueKind == JsonValueKind.Null ? null : acc.GetDouble();

				_state = new ServerState(time, location, canStart, latitude, longitude, accuracy);
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
			Latitude_deg = state.Latitude_deg,
			Longitude_deg = state.Longitude_deg,
			Accuracy_m = state.Accuracy_m,
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
		_state = new ServerState(0, double.NaN, false, null, null, null);
		while (_httpQueryLog.TryDequeue(out _)) { }
		while (_receivedRequests.TryDequeue(out _)) { }
		lock (_infoLock)
		{
			_serverInfo = new ServerInfoState(
				Name: "TRViS Reference Server",
				Admin: null,
				Version: "0.0.0",
				ProtocolVersion: "1.0"
			);
			_diagrams.Clear();
			_currentDiagramId = null;
		}
		return OkJson("{\"ok\":true}");
	}

	// ================================================================
	// サーバー情報
	// ================================================================

	private HttpResponse GetServerInfo()
	{
		ServerInfoState info;
		lock (_infoLock) { info = _serverInfo; }
		return OkJson(JsonSerializer.Serialize(new
		{
			Name = info.Name,
			Admin = info.Admin,
			Version = info.Version,
			ProtocolVersion = info.ProtocolVersion,
		}, JsonOptions));
	}

	private HttpResponse SetServerInfo(HttpRequest request)
	{
		try
		{
			string body = Encoding.UTF8.GetString(request.Body);
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;
			lock (_infoLock)
			{
				_serverInfo = new ServerInfoState(
					Name: TryGetString(root, "Name") ?? _serverInfo.Name,
					Admin: TryGetString(root, "Admin") ?? _serverInfo.Admin,
					Version: TryGetString(root, "Version") ?? _serverInfo.Version,
					ProtocolVersion: TryGetString(root, "ProtocolVersion") ?? _serverInfo.ProtocolVersion
				);
			}
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex)
		{
			return Error(HttpStatusCode.BadRequest, ex.Message);
		}
	}

	private async Task<HttpResponse> BroadcastServerInfoAsync()
	{
		string message = BuildServerInfoMessage();
		await BroadcastTextAsync(message);
		return OkJson("{\"ok\":true}");
	}

	private string BuildServerInfoMessage()
	{
		ServerInfoState info;
		lock (_infoLock) { info = _serverInfo; }
		return JsonSerializer.Serialize(new
		{
			MessageType = "ServerInfo",
			Name = info.Name,
			Admin = info.Admin,
			Version = info.Version,
			ProtocolVersion = info.ProtocolVersion,
		});
	}

	// ================================================================
	// ダイヤ情報
	// ================================================================

	private HttpResponse GetDiagrams()
	{
		DiagramInfoState[] arr;
		string? current;
		lock (_infoLock)
		{
			arr = _diagrams.Values.ToArray();
			current = _currentDiagramId;
		}
		return OkJson(JsonSerializer.Serialize(new
		{
			CurrentDiagramId = current,
			Diagrams = arr.Select(d => new
			{
				Id = d.Id,
				Name = d.Name,
				Description = d.Description,
				WorkGroupIds = d.WorkGroupIds,
			}).ToArray(),
		}, JsonOptions));
	}

	/// <summary>
	/// ダイヤ情報を登録/更新する。
	/// リクエストボディ: { Id, Name?, Description?, WorkGroupIds?: string[], MakeCurrent?: bool }
	/// </summary>
	private HttpResponse SetDiagram(HttpRequest request)
	{
		try
		{
			string body = Encoding.UTF8.GetString(request.Body);
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;

			string? id = TryGetString(root, "Id");
			if (string.IsNullOrEmpty(id))
				return Error(HttpStatusCode.BadRequest, "Missing 'Id' field");

			string? name = TryGetString(root, "Name");
			string? description = TryGetString(root, "Description");
			string[]? wgIds = null;
			if (root.TryGetProperty("WorkGroupIds", out var wgIdsProp) && wgIdsProp.ValueKind == JsonValueKind.Array)
			{
				wgIds = wgIdsProp.EnumerateArray()
					.Where(e => e.ValueKind == JsonValueKind.String)
					.Select(e => e.GetString()!)
					.ToArray();
			}
			bool makeCurrent = root.TryGetProperty("MakeCurrent", out var mc)
				&& mc.ValueKind == JsonValueKind.True;

			lock (_infoLock)
			{
				_diagrams[id] = new DiagramInfoState(id, name, description, wgIds);
				if (makeCurrent || _currentDiagramId is null)
					_currentDiagramId = id;
			}
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex)
		{
			return Error(HttpStatusCode.BadRequest, ex.Message);
		}
	}

	/// <summary>
	/// ダイヤ情報を全 WebSocket クライアントへブロードキャストする。
	/// クエリパラメータ "id" でダイヤを指定可能 (省略時はカレントダイヤ)。
	/// </summary>
	private async Task<HttpResponse> BroadcastDiagramAsync(HttpRequest request)
	{
		string? id = request.QueryString["id"];
		var msg = BuildDiagramInfoMessage(id);
		if (msg is null)
			return Error(HttpStatusCode.NotFound, $"No diagram for id={id ?? "(current)"}");
		await BroadcastTextAsync(msg);
		return OkJson("{\"ok\":true}");
	}

	private string? BuildDiagramInfoMessage(string? id)
	{
		DiagramInfoState? info;
		lock (_infoLock)
		{
			string? targetId = id ?? _currentDiagramId;
			if (targetId is null || !_diagrams.TryGetValue(targetId, out info))
				return null;
		}
		return JsonSerializer.Serialize(new
		{
			MessageType = "DiagramInfo",
			DiagramId = info.Id,
			Name = info.Name,
			Description = info.Description,
			WorkGroupIds = info.WorkGroupIds,
		});
	}

	private HttpResponse GetReceivedRequests()
		=> OkJson(JsonSerializer.Serialize(_receivedRequests.ToArray(), JsonOptions));

	private HttpResponse ClearReceivedRequests()
	{
		while (_receivedRequests.TryDequeue(out _)) { }
		return OkJson("{\"ok\":true}");
	}

	// ================================================================
	// リモートコマンド配信
	// ================================================================

	/// <summary>
	/// SelectTrain: { WorkGroupId?, WorkId?, TrainId? }
	/// </summary>
	private async Task<HttpResponse> BroadcastSelectTrainAsync(HttpRequest request)
	{
		try
		{
			using var doc = JsonDocument.Parse(request.Body.Length > 0
				? Encoding.UTF8.GetString(request.Body)
				: "{}");
			var root = doc.RootElement;
			var msg = JsonSerializer.Serialize(new
			{
				MessageType = "SelectTrain",
				WorkGroupId = TryGetString(root, "WorkGroupId"),
				WorkId = TryGetString(root, "WorkId"),
				TrainId = TryGetString(root, "TrainId"),
			});
			await BroadcastTextAsync(msg);
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex) { return Error(HttpStatusCode.BadRequest, ex.Message); }
	}

	/// <summary>
	/// OperationCommand: { Action: "StartOperation" | "EndOperation" | "EnableLocationService" | "DisableLocationService" }
	/// </summary>
	private async Task<HttpResponse> BroadcastOperationCommandAsync(HttpRequest request)
	{
		try
		{
			string body = Encoding.UTF8.GetString(request.Body);
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;
			string? action = TryGetString(root, "Action");
			if (string.IsNullOrEmpty(action))
				return Error(HttpStatusCode.BadRequest, "Missing 'Action' field");
			var msg = JsonSerializer.Serialize(new
			{
				MessageType = "OperationCommand",
				Action = action,
			});
			await BroadcastTextAsync(msg);
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex) { return Error(HttpStatusCode.BadRequest, ex.Message); }
	}

	/// <summary>
	/// HeaderColor: { ResetToDefault?: bool, Color_RGB?: int (0xRRGGBB) }
	/// </summary>
	private async Task<HttpResponse> BroadcastHeaderColorAsync(HttpRequest request)
	{
		try
		{
			string body = Encoding.UTF8.GetString(request.Body);
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;
			bool reset = root.TryGetProperty("ResetToDefault", out var r)
				&& r.ValueKind == JsonValueKind.True;
			int? color = null;
			if (!reset && root.TryGetProperty("Color_RGB", out var c)
				&& c.ValueKind == JsonValueKind.Number
				&& c.TryGetInt32(out int rgb))
				color = rgb;
			var msg = JsonSerializer.Serialize(new
			{
				MessageType = "HeaderColor",
				ResetToDefault = reset,
				Color_RGB = color,
			});
			await BroadcastTextAsync(msg);
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex) { return Error(HttpStatusCode.BadRequest, ex.Message); }
	}

	/// <summary>
	/// Notification: { Id?, Title?, Body?, Priority?, IssuedAt? (ISO8601) }
	/// </summary>
	private async Task<HttpResponse> BroadcastNotificationAsync(HttpRequest request)
	{
		try
		{
			string body = Encoding.UTF8.GetString(request.Body);
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;
			int priority = 0;
			if (root.TryGetProperty("Priority", out var p) && p.ValueKind == JsonValueKind.Number
				&& p.TryGetInt32(out int prio))
				priority = prio;
			string? issuedAt = TryGetString(root, "IssuedAt");
			var msg = JsonSerializer.Serialize(new
			{
				MessageType = "Notification",
				Id = TryGetString(root, "Id"),
				Title = TryGetString(root, "Title"),
				Body = TryGetString(root, "Body"),
				Priority = priority,
				IssuedAt = issuedAt,
			});
			await BroadcastTextAsync(msg);
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex) { return Error(HttpStatusCode.BadRequest, ex.Message); }
	}

	/// <summary>
	/// TimeFormat: { Format?: string (例 "HH:mm" / "HH:mm:ss") }
	/// Format が省略 / null の場合は「端末の既定にリセット」を意味する。
	/// </summary>
	private async Task<HttpResponse> BroadcastTimeFormatAsync(HttpRequest request)
	{
		try
		{
			string body = request.Body.Length > 0 ? Encoding.UTF8.GetString(request.Body) : "{}";
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;
			string? format = TryGetString(root, "Format");
			var msg = JsonSerializer.Serialize(new
			{
				MessageType = "TimeFormat",
				Format = format,
			});
			await BroadcastTextAsync(msg);
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex) { return Error(HttpStatusCode.BadRequest, ex.Message); }
	}

	private async Task<HttpResponse> BroadcastNavigateToHomeAsync()
	{
		var msg = JsonSerializer.Serialize(new { MessageType = "NavigateToHome" });
		await BroadcastTextAsync(msg);
		return OkJson("{\"ok\":true}");
	}

	/// <summary>
	/// OpenTimetable: { WorkGroupId?, WorkId?, TrainId? }
	/// </summary>
	private async Task<HttpResponse> BroadcastOpenTimetableAsync(HttpRequest request)
	{
		try
		{
			using var doc = JsonDocument.Parse(request.Body.Length > 0
				? Encoding.UTF8.GetString(request.Body)
				: "{}");
			var root = doc.RootElement;
			var msg = JsonSerializer.Serialize(new
			{
				MessageType = "OpenTimetable",
				WorkGroupId = TryGetString(root, "WorkGroupId"),
				WorkId = TryGetString(root, "WorkId"),
				TrainId = TryGetString(root, "TrainId"),
			});
			await BroadcastTextAsync(msg);
			return OkJson("{\"ok\":true}");
		}
		catch (JsonException ex) { return Error(HttpStatusCode.BadRequest, ex.Message); }
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
					await ProcessClientMessageAsync(state, msg.GetText());
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

	/// <summary>
	/// クライアントから受信した WS メッセージを処理する。
	/// MessageType フィールドが指定されていれば該当の要求として扱い、
	/// なければ ID 更新メッセージとして扱う (後方互換)。
	/// </summary>
	private async Task ProcessClientMessageAsync(ClientState client, string message)
	{
		try
		{
			using var doc = JsonDocument.Parse(message);
			var root = doc.RootElement;

			// MessageType による要求ハンドリング
			if (root.TryGetProperty("MessageType", out var mt) && mt.ValueKind == JsonValueKind.String)
			{
				string? messageType = mt.GetString();
				switch (messageType)
				{
					case "RequestServerInfo":
						_receivedRequests.Enqueue(new ReceivedRequestDto(
							ConnectionId: client.ConnectionId,
							MessageType: messageType,
							DiagramId: null,
							ReceivedAt: DateTime.UtcNow));
						await TrySendAsync(client.Connection, BuildServerInfoMessage());
						return;
					case "RequestDiagramInfo":
						{
							string? diagramId = TryGetString(root, "DiagramId");
							_receivedRequests.Enqueue(new ReceivedRequestDto(
								ConnectionId: client.ConnectionId,
								MessageType: messageType,
								DiagramId: diagramId,
								ReceivedAt: DateTime.UtcNow));
							var resp = BuildDiagramInfoMessage(diagramId);
							if (resp is not null)
								await TrySendAsync(client.Connection, resp);
							return;
						}
				}
			}

			// 後方互換: MessageType が無い場合は ID 更新メッセージとして扱う
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

	private sealed record ServerInfoState(string? Name, string? Admin, string? Version, string? ProtocolVersion);
	private sealed record DiagramInfoState(string Id, string? Name, string? Description, string[]? WorkGroupIds);
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

public sealed record ReceivedRequestDto(
	string ConnectionId,
	string? MessageType,
	string? DiagramId,
	DateTime ReceivedAt
);
