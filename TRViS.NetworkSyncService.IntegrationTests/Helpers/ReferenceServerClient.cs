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
		CancellationToken ct = default)
	{
		var payload = new { Time_ms = time_ms, Location_m = location_m, CanStart = canStart };
		var content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
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
	[property: JsonPropertyName("Time_ms")] long Time_ms,
	[property: JsonPropertyName("Location_m")] double? Location_m,
	[property: JsonPropertyName("CanStart")] bool CanStart
);
