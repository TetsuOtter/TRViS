using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using NLog;

namespace TRViS.NetworkSyncService;

/// <summary>
/// Base class for NetworkSyncService manager that handles common functionality
/// for both HTTP and WebSocket implementations
/// </summary>
public static class NetworkSyncServiceUtil
{
	private static readonly Logger logger = LogManager.GetCurrentClassLogger();

	public static async Task<HttpNetworkSyncService> CreateFromUriAsync(Uri uri, HttpClient? httpClient = null, CancellationToken? cancellationToken = null)
	{
		cancellationToken ??= CancellationToken.None;
		httpClient ??= new HttpClient();
		// 将来的にはWebSocket, BIDSも対応したい
		logger.Debug("CreateFromUriAsync: Sending preflight request to {0}", uri);
		HttpResponseMessage preflight = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken.Value);
		// 将来的にはNetworkSyncServiceのバージョン情報を取得して、互換性を確認する
		if (!preflight.IsSuccessStatusCode)
		{
			logger.Error("CreateFromUriAsync: Preflight request failed with status code: {0}", preflight.StatusCode);
			throw new InvalidOperationException("Failed to connect to the NetworkSyncService server.");
		}
		logger.Info("CreateFromUriAsync: Successfully created HttpNetworkSyncService");
		return new HttpNetworkSyncService(uri, httpClient);
	}

	public static async Task<WebSocketNetworkSyncService> CreateFromWebSocketAsync(Uri uri, ClientWebSocket? webSocket = null, CancellationToken? cancellationToken = null)
	{
		cancellationToken ??= CancellationToken.None;
		webSocket ??= new ClientWebSocket();

		// ws:// または wss:// スキームを確認
		if (uri.Scheme != "ws" && uri.Scheme != "wss")
		{
			logger.Error("CreateFromWebSocketAsync: Invalid URI scheme: {0}", uri.Scheme);
			throw new ArgumentException("URI must use ws:// or wss:// scheme for WebSocket connections.", nameof(uri));
		}

		WebSocketNetworkSyncService manager = new(uri, webSocket);
		await manager.ConnectAsync(cancellationToken.Value);

		return manager;
	}

	public static async Task<NetworkSyncServiceBase> CreateAsync(Uri uri, HttpClient? httpClient = null, ClientWebSocket? webSocket = null, CancellationToken? cancellationToken = null)
	{
		// スキームに基づいて適切なプロバイダーを選択
		if (uri.Scheme == "ws" || uri.Scheme == "wss")
		{
			logger.Info("CreateAsync: Using WebSocket for URI: {0}", uri);
			return await CreateFromWebSocketAsync(uri, webSocket, cancellationToken);
		}
		else
		{
			logger.Info("CreateAsync: Using HTTP for URI: {0}", uri);
			return await CreateFromUriAsync(uri, httpClient, cancellationToken);
		}
	}
}
