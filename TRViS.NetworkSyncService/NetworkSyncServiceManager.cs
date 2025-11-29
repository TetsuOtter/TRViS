using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using TRViS.Services;
using TRViS.NetworkSyncService.DataProviders;

namespace TRViS.NetworkSyncService;

public class NetworkSyncServiceManager : ILocationService, IDisposable
{
	public bool IsEnabled { get; set; }
	private bool _CanUseService = false;
	public bool CanUseService
	{
		get => _CanUseService;
		private set
		{
			if (_CanUseService == value)
				return;

			_CanUseService = value;
			CanUseServiceChanged?.Invoke(this, value);
		}
	}

	private StaLocationInfo[]? _staLocationInfo;
	public StaLocationInfo[]? StaLocationInfo
	{
		get => _staLocationInfo;
		set
		{
			if (_staLocationInfo == value)
				return;

			_staLocationInfo = value;
			ResetLocationInfo();
		}
	}

	public string? WorkGroupId { get => _DataProvider.WorkGroupId; set => _DataProvider.WorkGroupId = value; }
	public string? WorkId { get => _DataProvider.WorkId; set => _DataProvider.WorkId = value; }
	public string? TrainId { get => _DataProvider.TrainId; set => _DataProvider.TrainId = value; }

	public int CurrentStationIndex { get; private set; }

	public bool IsRunningToNextStation { get; private set; }

	public bool IsTimeServiceEnabled { get; private set; }
	public int CurrentTime_s { get; private set; }

	public event EventHandler<bool>? CanUseServiceChanged;
	public event EventHandler<LocationStateChangedEventArgs>? LocationStateChanged;
	public event EventHandler<int>? TimeChanged;

	private readonly IDataProvider _DataProvider;
	private bool _IsDisposed;

	private NetworkSyncServiceManager(IDataProvider dataProvider)
	{
		_DataProvider = dataProvider;
	}

	public static async Task<NetworkSyncServiceManager> CreateFromUriAsync(Uri uri, HttpClient? httpClient = null, CancellationToken? cancellationToken = null)
	{
		cancellationToken ??= CancellationToken.None;
		httpClient ??= new HttpClient();
		// 将来的にはWebSocket, BIDSも対応したい
		HttpResponseMessage preflight = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken.Value);
		// 将来的にはNetworkSyncServiceのバージョン情報を取得して、互換性を確認する
		if (!preflight.IsSuccessStatusCode)
			throw new InvalidOperationException("Failed to connect to the NetworkSyncService server.");
		return new(new HttpDataProvider(uri, httpClient));
	}

	public static async Task<NetworkSyncServiceManager> CreateFromWebSocketAsync(Uri uri, ClientWebSocket? webSocket = null, CancellationToken? cancellationToken = null)
	{
		cancellationToken ??= CancellationToken.None;
		webSocket ??= new ClientWebSocket();

		// ws:// または wss:// スキームを確認
		if (uri.Scheme != "ws" && uri.Scheme != "wss")
			throw new ArgumentException("URI must use ws:// or wss:// scheme for WebSocket connections.", nameof(uri));

		WebSocketDataProvider provider = new(uri, webSocket);
		await provider.ConnectAsync(cancellationToken.Value);

		return new(provider);
	}

	public static async Task<NetworkSyncServiceManager> CreateAsync(Uri uri, HttpClient? httpClient = null, ClientWebSocket? webSocket = null, CancellationToken? cancellationToken = null)
	{
		// スキームに基づいて適切なプロバイダーを選択
		if (uri.Scheme == "ws" || uri.Scheme == "wss")
		{
			return await CreateFromWebSocketAsync(uri, webSocket, cancellationToken);
		}
		else
		{
			return await CreateFromUriAsync(uri, httpClient, cancellationToken);
		}
	}

	public async Task TickAsync(CancellationToken? cancellationToken = null)
	{
		cancellationToken ??= CancellationToken.None;
		SyncedData result = await _DataProvider.GetSyncedDataAsync(cancellationToken.Value);

		UpdateCurrentStationWithLocation(result.Location_m);

		int currentTime_s = (int)(result.Time_ms / 1000);
		if (CurrentTime_s != currentTime_s)
		{
			CurrentTime_s = currentTime_s;
			TimeChanged?.Invoke(this, CurrentTime_s);
		}

		CanUseService = result.CanStart;
	}

	void UpdateCurrentStationWithLocation(double location_m)
	{
		if (StaLocationInfo is null || !IsEnabled || double.IsNaN(location_m))
			return;

		bool isIn(double threshold1, double threshold2)
		{
			if (threshold1 < threshold2)
				return threshold1 <= location_m && location_m < threshold2;
			else
				return threshold2 <= location_m && location_m < threshold1;
		}

		// 距離が逆戻りする可能性は考えない
		for (int i = 0; i < StaLocationInfo.Length; i++)
		{
			double staLocation_m = StaLocationInfo[i].Location_m;
			double staNearbyRadius_m = StaLocationInfo[i].NearbyRadius_m;
			if (isIn(staLocation_m - staNearbyRadius_m, staLocation_m + staNearbyRadius_m))
			{
				if (i != CurrentStationIndex || IsRunningToNextStation)
					ForceSetLocationInfo(i, false);
				return;
			}
			else if (i != 0 && isIn(StaLocationInfo[i - 1].Location_m, staLocation_m))
			{
				if (i - 1 != CurrentStationIndex || !IsRunningToNextStation)
					ForceSetLocationInfo(i - 1, true);
				return;
			}
		}

		double distanceFromFirstStation = Math.Abs(location_m - StaLocationInfo[0].Location_m);
		double distanceFromLastStation = Math.Abs(location_m - StaLocationInfo[^1].Location_m);
		if (distanceFromFirstStation < distanceFromLastStation)
		{
			if (0 != CurrentStationIndex || IsRunningToNextStation)
				ForceSetLocationInfo(0, false);
		}
		else
		{
			if (StaLocationInfo.Length - 1 != CurrentStationIndex || IsRunningToNextStation)
				ForceSetLocationInfo(StaLocationInfo.Length - 1, false);
		}
	}

	public void ForceSetLocationInfo(
		int stationIndex,
		bool isRunningToNextStation
	)
	{
		CurrentStationIndex = stationIndex;
		IsRunningToNextStation = isRunningToNextStation;
		LocationStateChanged?.Invoke(this, new LocationStateChangedEventArgs(CurrentStationIndex, IsRunningToNextStation));
	}

	public void ResetLocationInfo()
	{
		CurrentStationIndex = 0;
		IsRunningToNextStation = false;
		LocationStateChanged?.Invoke(this, new LocationStateChangedEventArgs(CurrentStationIndex, IsRunningToNextStation));
	}

	public void Dispose()
	{
		if (_IsDisposed)
			return;

		_IsDisposed = true;

		if (_DataProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}
}
