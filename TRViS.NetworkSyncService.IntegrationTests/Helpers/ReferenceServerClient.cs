using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using TRViS.ReferenceServer;

namespace TRViS.NetworkSyncService.IntegrationTests.Helpers;

/// <summary>
/// リファレンスサーバーの Control API に対して HTTP リクエストを送るヘルパークラス。
/// ローカル (in-process) / Docker コンテナのどちらに対しても同一コードで動作する。
/// </summary>
public sealed class ReferenceServerClient : IDisposable
{
	private readonly HttpClient _http;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	public ReferenceServerClient(string baseUrl)
	{
		_http = new HttpClient { BaseAddress = new Uri(baseUrl) };
	}

	public void Dispose() => _http.Dispose();

	// ================================================================
	// 状態管理
	// ================================================================

	public async Task<ServerStateDto> GetStateAsync(CancellationToken ct = default)
	{
		var resp = await _http.GetAsync("/control/state", ct);
		resp.EnsureSuccessStatusCode();
		return JsonSerializer.Deserialize<ServerStateDto>(
			await resp.Content.ReadAsStringAsync(ct), JsonOptions)!;
	}

	public async Task SetStateAsync(
		long? time_ms = null,
		double? location_m = null,
		bool? canStart = null,
		double? latitude_deg = null,
		double? longitude_deg = null,
		double? accuracy_m = null,
		CancellationToken ct = default)
	{
		var payload = new
		{
			Time_ms = time_ms,
			Location_m = location_m,
			CanStart = canStart,
			Latitude_deg = latitude_deg,
			Longitude_deg = longitude_deg,
			Accuracy_m = accuracy_m,
		};
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/state", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	/// <summary>
	/// Time_ms を明示的に JSON null にリセットする (#308 回帰テスト用)。
	/// <see cref="SetStateAsync"/> は <see cref="JsonIgnoreCondition.WhenWritingNull"/> により
	/// null 引数のプロパティを送信 JSON から除外してしまうため、"未指定" と "明示的な null" を
	/// 区別できない。このメソッドはリテラル null を送るための専用ヘルパー。
	/// </summary>
	public async Task SetStateTimeMsNullAsync(CancellationToken ct = default)
	{
		var content = new StringContent("{\"Time_ms\":null}", Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/state", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task ResetAsync(CancellationToken ct = default)
	{
		var resp = await _http.PostAsync("/control/reset", null, ct);
		resp.EnsureSuccessStatusCode();
	}

	// ================================================================
	// ブロードキャスト
	// ================================================================

	public async Task BroadcastSyncedDataAsync(CancellationToken ct = default)
	{
		var resp = await _http.PostAsync("/control/broadcast-synced", null, ct);
		resp.EnsureSuccessStatusCode();
	}

	/// <summary>
	/// 時刻表データを全 WebSocket クライアントに配信する。
	/// <paramref name="dataJson"/> は JSON 文字列 (WorkGroupData[] など) を渡す。
	/// スコープは WorkGroupId / WorkId / TrainId の指定有無で自動判定される。
	/// </summary>
	public async Task BroadcastTimetableAsync(
		string dataJson,
		string? workGroupId = null,
		string? workId = null,
		string? trainId = null,
		CancellationToken ct = default)
	{
		var payload = new
		{
			WorkGroupId = workGroupId,
			WorkId = workId,
			TrainId = trainId,
			Data = dataJson,
		};
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-timetable", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	// ================================================================
	// HTTP クエリログ
	// ================================================================

	public async Task<List<ReceivedHttpQueryDto>> GetHttpQueriesAsync(CancellationToken ct = default)
	{
		var resp = await _http.GetAsync("/control/http-queries", ct);
		resp.EnsureSuccessStatusCode();
		return JsonSerializer.Deserialize<List<ReceivedHttpQueryDto>>(
			await resp.Content.ReadAsStringAsync(ct), JsonOptions)!;
	}

	public async Task ClearHttpQueriesAsync(CancellationToken ct = default)
	{
		using var req = new HttpRequestMessage(HttpMethod.Delete, "/control/http-queries");
		var resp = await _http.SendAsync(req, ct);
		resp.EnsureSuccessStatusCode();
	}

	// ================================================================
	// WebSocket クライアント情報
	// ================================================================

	public async Task<List<WsClientDto>> GetWsClientsAsync(CancellationToken ct = default)
	{
		var resp = await _http.GetAsync("/control/ws-clients", ct);
		resp.EnsureSuccessStatusCode();
		return JsonSerializer.Deserialize<List<WsClientDto>>(
			await resp.Content.ReadAsStringAsync(ct), JsonOptions)!;
	}

	public async Task DisconnectAllClientsAsync(CancellationToken ct = default)
	{
		var resp = await _http.PostAsync("/control/disconnect-all", null, ct);
		resp.EnsureSuccessStatusCode();
	}

	// ================================================================
	// サーバー情報・ダイヤ情報
	// ================================================================

	public async Task<ServerInfoDto> GetServerInfoAsync(CancellationToken ct = default)
	{
		var resp = await _http.GetAsync("/control/server-info", ct);
		resp.EnsureSuccessStatusCode();
		return JsonSerializer.Deserialize<ServerInfoDto>(
			await resp.Content.ReadAsStringAsync(ct), JsonOptions)!;
	}

	public async Task SetServerInfoAsync(
		string? name = null,
		string? admin = null,
		string? version = null,
		string? protocolVersion = null,
		string[]? features = null,
		string? iconImage = null,
		string? iconImageDark = null,
		CancellationToken ct = default)
	{
		var payload = new
		{
			Name = name,
			Admin = admin,
			Version = version,
			ProtocolVersion = protocolVersion,
			Features = features,
			IconImage = iconImage,
			IconImageDark = iconImageDark,
		};
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/server-info", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	// ================================================================
	// 列車検索データセット
	// ================================================================

	/// <summary>
	/// 検索可能列車データセットを差し替える。各 <see cref="SearchTrainSeed"/> の
	/// <c>DataJson</c> は Train スコープの TrainData JSON 文字列。
	/// </summary>
	public async Task SetSearchTrainsAsync(IEnumerable<SearchTrainSeed> trains, CancellationToken ct = default)
	{
		var payload = new
		{
			Trains = trains.Select(t => new
			{
				t.WorkGroupId,
				t.WorkId,
				t.TrainId,
				t.TrainNumber,
				t.WorkName,
				t.Direction,
				t.StartStationName,
				t.StartTime,
				t.EndStationName,
				t.EndTime,
				Data = t.DataJson,
			}),
		};
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/search-trains", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastServerInfoAsync(CancellationToken ct = default)
	{
		var resp = await _http.PostAsync("/control/broadcast-server-info", null, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task<DiagramListDto> GetDiagramsAsync(CancellationToken ct = default)
	{
		var resp = await _http.GetAsync("/control/diagrams", ct);
		resp.EnsureSuccessStatusCode();
		return JsonSerializer.Deserialize<DiagramListDto>(
			await resp.Content.ReadAsStringAsync(ct), JsonOptions)!;
	}

	public async Task SetDiagramAsync(
		string id,
		string? name = null,
		string? description = null,
		string[]? workGroupIds = null,
		bool makeCurrent = false,
		CancellationToken ct = default)
	{
		var payload = new
		{
			Id = id,
			Name = name,
			Description = description,
			WorkGroupIds = workGroupIds,
			MakeCurrent = makeCurrent,
		};
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/diagrams", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastDiagramAsync(string? diagramId = null, CancellationToken ct = default)
	{
		string url = diagramId is null
			? "/control/broadcast-diagram"
			: $"/control/broadcast-diagram?id={Uri.EscapeDataString(diagramId)}";
		var resp = await _http.PostAsync(url, null, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task<List<ReceivedRequestDto>> GetReceivedRequestsAsync(CancellationToken ct = default)
	{
		var resp = await _http.GetAsync("/control/received-requests", ct);
		resp.EnsureSuccessStatusCode();
		return JsonSerializer.Deserialize<List<ReceivedRequestDto>>(
			await resp.Content.ReadAsStringAsync(ct), JsonOptions)!;
	}

	public async Task ClearReceivedRequestsAsync(CancellationToken ct = default)
	{
		using var req = new HttpRequestMessage(HttpMethod.Delete, "/control/received-requests");
		var resp = await _http.SendAsync(req, ct);
		resp.EnsureSuccessStatusCode();
	}

	// ================================================================
	// リモートコマンド配信
	// ================================================================

	public async Task BroadcastSelectTrainAsync(
		string? workGroupId = null,
		string? workId = null,
		string? trainId = null,
		CancellationToken ct = default)
	{
		var payload = new { WorkGroupId = workGroupId, WorkId = workId, TrainId = trainId };
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-select-train", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastOperationCommandAsync(string action, CancellationToken ct = default)
	{
		var payload = new { Action = action };
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-operation-command", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastHeaderColorAsync(
		bool resetToDefault = false,
		int? color_RGB = null,
		CancellationToken ct = default)
	{
		var payload = new { ResetToDefault = resetToDefault, Color_RGB = color_RGB };
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-header-color", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastNotificationAsync(
		string? id = null,
		string? title = null,
		string? body = null,
		int priority = 0,
		string? issuedAt = null,
		bool acknowledged = false,
		string? orderNumber = null,
		string? receiver = null,
		string? sender = null,
		string? iconText = null,
		int? iconColor_RGB = null,
		string? iconColorHex = null,
		string? iconImageBase64 = null,
		string? receivedSoundBase64 = null,
		string? receivedSoundFormat = null,
		string? approachSoundBase64 = null,
		string? approachSoundFormat = null,
		CancellationToken ct = default)
	{
		// IconColor_RGB は数値 (0xRRGGBB) と "#RRGGBB" 文字列のどちらでも送れる。
		// iconColorHex が指定されていればそちらを優先する。
		object? iconColor = iconColorHex is not null ? iconColorHex : iconColor_RGB;
		var payload = new
		{
			Id = id,
			OrderNumber = orderNumber,
			Title = title,
			Body = body,
			Priority = priority,
			IssuedAt = issuedAt,
			Receiver = receiver,
			Sender = sender,
			IconText = iconText,
			IconColor_RGB = iconColor,
			IconImageBase64 = iconImageBase64,
			Acknowledged = acknowledged,
			ReceivedSoundBase64 = receivedSoundBase64,
			ReceivedSoundFormat = receivedSoundFormat,
			ApproachSoundBase64 = approachSoundBase64,
			ApproachSoundFormat = approachSoundFormat,
		};
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-notification", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastNotificationDeleteAsync(string id, CancellationToken ct = default)
	{
		var payload = new { Id = id };
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-notification-delete", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastDefaultSoundAsync(
		string? receivedSoundBase64 = null,
		string? receivedSoundFormat = null,
		string? approachSoundBase64 = null,
		string? approachSoundFormat = null,
		CancellationToken ct = default)
	{
		var payload = new
		{
			ReceivedSoundBase64 = receivedSoundBase64,
			ReceivedSoundFormat = receivedSoundFormat,
			ApproachSoundBase64 = approachSoundBase64,
			ApproachSoundFormat = approachSoundFormat,
		};
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-default-sound", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastTimeFormatAsync(string? format = null, CancellationToken ct = default)
	{
		var payload = new { Format = format };
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-time-format", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastNavigateToHomeAsync(CancellationToken ct = default)
	{
		var resp = await _http.PostAsync("/control/broadcast-navigate-to-home", null, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task BroadcastOpenTimetableAsync(
		string? workGroupId = null,
		string? workId = null,
		string? trainId = null,
		CancellationToken ct = default)
	{
		var payload = new { WorkGroupId = workGroupId, WorkId = workId, TrainId = trainId };
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
		var resp = await _http.PostAsync("/control/broadcast-open-timetable", content, ct);
		resp.EnsureSuccessStatusCode();
	}

	// ================================================================
	// ユーティリティ
	// ================================================================

	/// <summary>
	/// サーバーが起動して応答するまでポーリングして待機する。
	/// </summary>
	public async Task WaitForReadyAsync(TimeSpan? timeout = null, CancellationToken ct = default)
	{
		var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
		while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
		{
			try
			{
				var resp = await _http.GetAsync("/health", ct);
				if (resp.IsSuccessStatusCode) return;
			}
			catch { }
			await Task.Delay(200, ct);
		}
		throw new TimeoutException("Reference server did not become ready in time.");
	}

	/// <summary>
	/// 指定条件が満たされるまでポーリングして待機する。
	/// </summary>
	public static async Task WaitForConditionAsync(
		Func<Task<bool>> condition,
		int timeoutMs = 5000,
		int pollIntervalMs = 100,
		CancellationToken ct = default)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
		{
			if (await condition()) return;
			await Task.Delay(pollIntervalMs, ct);
		}
		throw new TimeoutException($"Condition not met within {timeoutMs}ms.");
	}
}

// ================================================================
// DTO (Control API レスポンス)
// ================================================================

public sealed record ServerStateDto(
	[property: JsonPropertyName("Time_ms")] long? Time_ms,
	[property: JsonPropertyName("Location_m")] double? Location_m,
	[property: JsonPropertyName("CanStart")] bool CanStart,
	[property: JsonPropertyName("Latitude_deg")] double? Latitude_deg = null,
	[property: JsonPropertyName("Longitude_deg")] double? Longitude_deg = null,
	[property: JsonPropertyName("Accuracy_m")] double? Accuracy_m = null
);

public sealed record ServerInfoDto(
	[property: JsonPropertyName("Name")] string? Name,
	[property: JsonPropertyName("Admin")] string? Admin,
	[property: JsonPropertyName("Version")] string? Version,
	[property: JsonPropertyName("ProtocolVersion")] string? ProtocolVersion,
	[property: JsonPropertyName("Features")] string[]? Features = null
);

/// <summary>
/// <see cref="ReferenceServerClient.SetSearchTrainsAsync"/> に渡す検索可能列車のシード。
/// </summary>
public sealed record SearchTrainSeed(
	string WorkGroupId,
	string WorkId,
	string TrainId,
	string TrainNumber,
	string DataJson,
	string? WorkName = null,
	int? Direction = null,
	string? StartStationName = null,
	string? StartTime = null,
	string? EndStationName = null,
	string? EndTime = null
);

public sealed record DiagramListDto(
	[property: JsonPropertyName("CurrentDiagramId")] string? CurrentDiagramId,
	[property: JsonPropertyName("Diagrams")] DiagramEntryDto[] Diagrams
);

public sealed record DiagramEntryDto(
	[property: JsonPropertyName("Id")] string Id,
	[property: JsonPropertyName("Name")] string? Name,
	[property: JsonPropertyName("Description")] string? Description,
	[property: JsonPropertyName("WorkGroupIds")] string[]? WorkGroupIds
);
