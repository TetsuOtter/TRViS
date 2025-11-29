using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace TRViS.NetworkSyncService;

/// <summary>
/// HTTP-based implementation of NetworkSyncService
/// </summary>
public class HttpNetworkSyncService : NetworkSyncServiceBase
{
	private const string WORK_GROUP_ID_QUERY_KEY = "workgroup";
	private const string WORK_ID_QUERY_KEY = "work";
	private const string TRAIN_ID_QUERY_KEY = "train";

	private const string LOCATION_M_JSON_KEY = "Location_m";
	private const string TIME_MS_JSON_KEY = "Time_ms";
	private const string CAN_START_JSON_KEY = "CanStart";

	private readonly HttpClient _HttpClient;
	private readonly Uri _Uri;
	private readonly NameValueCollection BaseQuery;
	private Uri _NextUri;

	public HttpNetworkSyncService(Uri uri, HttpClient httpClient)
	{
		_Uri = uri;
		_HttpClient = httpClient;
		BaseQuery = HttpUtility.ParseQueryString(uri.Query);
		_NextUri = uri;
	}

	protected override void OnWorkGroupIdChanged(string? value)
	{
		UpdateNextUri();
	}

	protected override void OnWorkIdChanged(string? value)
	{
		UpdateNextUri();
	}

	protected override void OnTrainIdChanged(string? value)
	{
		UpdateNextUri();
	}

	private void UpdateNextUri()
	{
		NameValueCollection query = new(BaseQuery);
		if (WorkGroupId is not null)
			query[WORK_GROUP_ID_QUERY_KEY] = WorkGroupId;
		if (WorkId is not null)
			query[WORK_ID_QUERY_KEY] = WorkId;
		if (TrainId is not null)
			query[TRAIN_ID_QUERY_KEY] = TrainId;
		StringBuilder queryBuilder = new();
		bool isFirst = true;
		foreach (string? key in query.AllKeys)
		{
			if (key is null)
				continue;
			queryBuilder.Append(isFirst ? '?' : '&');
			queryBuilder.Append(key);
			queryBuilder.Append('=');
			queryBuilder.Append(query[key]);
			isFirst = false;
		}
		_NextUri = new UriBuilder(_Uri)
		{
			Query = queryBuilder.ToString()
		}.Uri;
	}

	protected override async Task<SyncedData> GetSyncedDataAsync(CancellationToken token)
	{
		using HttpResponseMessage response = await _HttpClient.GetAsync(_NextUri, token);
		// 接続に失敗等しない限り、成功として扱う
		// (ログ出力は今後検討)
		if (!response.IsSuccessStatusCode)
		{
			return new(
				Location_m: double.NaN,
				Time_ms: (long)DateTime.Now.TimeOfDay.TotalMilliseconds,
				CanStart: false
			);
		}

		using JsonDocument? json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(), cancellationToken: token);
		if (json is null)
		{
			return new(
				Location_m: double.NaN,
				Time_ms: (long)DateTime.Now.TimeOfDay.TotalMilliseconds,
				CanStart: false
			);
		}
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

		// Startできない状態が特殊なため、デフォルトでtrueとする
		bool canStart = true;
		try
		{
			canStart = root.GetProperty(CAN_START_JSON_KEY).GetBoolean();
		}
		catch (KeyNotFoundException) { }
		catch (FormatException) { }

		return new(
			Location_m: location_m,
			Time_ms: time_ms,
			CanStart: canStart
		);
	}

	public override void Dispose()
	{
		if (_IsDisposed)
			return;

		_IsDisposed = true;
	}
}
