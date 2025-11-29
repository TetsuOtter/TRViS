using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Web;

using TRViS.IO;
using TRViS.IO.RequestInfo;
using TRViS.NetworkSyncService;
using TRViS.Services;

namespace TRViS.ViewModels;

public enum AppLinkType
{
	Unknown,
	OpenFileJson,
	OpenFileSQLite,
};

public partial class AppViewModel
{
	const int EXTERNAL_RESOURCE_URL_HISTORY_MAX = 32;
	private readonly List<string> _ExternalResourceUrlHistory;
	public IReadOnlyList<string> ExternalResourceUrlHistory => _ExternalResourceUrlHistory;

	public async Task<bool> HandleAppLinkUriAsync(string uri, CancellationToken token)
	{
		AppLinkInfo appLinkInfo;
		try
		{
			appLinkInfo = AppLinkInfo.FromAppLink(uri);
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "AppLinkInfo Identify Failed");
			await Utils.DisplayAlert("Cannot Open File", "AppLinkInfo Identify Failed\n" + ex.Message, "OK");
			return false;
		}

		token.ThrowIfCancellationRequested();

		if (appLinkInfo.ResourceUri is not null && appLinkInfo.ResourceUri.Scheme is "http" or "https")
		{
			string path = appLinkInfo.ResourceUri.ToString();
			string decodedUrl = HttpUtility.UrlDecode(path);

			bool openRemoteFileCheckResult = await Utils.DisplayAlert(
				"外部ファイルを開く",
				$"ファイル `{decodedUrl}` を開きますか?",
				"はい",
				"いいえ"
			);
			logger.Info("Uri: {0} -> openFile: {1}", path, openRemoteFileCheckResult);
			if (!openRemoteFileCheckResult)
			{
				return false;
			}
		}

		token.ThrowIfCancellationRequested();

		return await HandleAppLinkUriAsync(appLinkInfo, uri, token);
	}
	public async Task<bool> HandleAppLinkUriAsync(AppLinkInfo appLinkInfo, CancellationToken token)
		=> await HandleAppLinkUriAsync(appLinkInfo, null, token);

	private async Task<bool> HandleAppLinkUriAsync(AppLinkInfo appLinkInfo, string? originalAppLink, CancellationToken token)
	{
		string? decodedUrl = null;
		string? appLinkString = originalAppLink;

		if (appLinkInfo.ResourceUri is not null && appLinkInfo.ResourceUri.Scheme is "http" or "https")
		{
			decodedUrl = HttpUtility.UrlDecode(appLinkInfo.ResourceUri.ToString());
		}

		token.ThrowIfCancellationRequested();

		// ResourceUriがWebSocket（ws:// or wss://）の場合、直接NetworkSyncServiceに接続
		if (appLinkInfo.ResourceUri is not null && appLinkInfo.ResourceUri.Scheme is "ws" or "wss")
		{
			logger.Info("ResourceUri is WebSocket -> Connect to NetworkSyncService directly");
			return await HandleWebSocketAppLinkAsync(appLinkInfo, appLinkString, token);
		}

		OpenFile openFile = new(InstanceManager.HttpClient)
		{
			CanContinueWhenResourceUriContainsIp = CanContinueWhenResourceUriContainsIpHandler,
			CanContinueWhenHeadRequestSuccess = CanContinueWhenHeadRequestSuccessHandler
		};
		ILoader loader;
		try
		{
			loader = await openFile.OpenAppLinkAsync(appLinkInfo, token);
		}
		catch (Exception ex)
		{
			if (ex is OperationCanceledException && ex is not TaskCanceledException)
			{
				logger.Debug(ex, "Operation Canceled");
				return false;
			}

			logger.Error(ex, "OpenAppLinkAsync Failed");
			if (appLinkInfo.ResourceUri?.HostNameType == UriHostNameType.IPv4
				&& ex is TaskCanceledException
				&& ex.InnerException is TimeoutException)
			{
				logger.Error(ex, "Timeout Error");
				await Utils.DisplayAlert(
					"接続できませんでした (Timeout)",
					"接続先がパソコンの場合は、\n"
					+ "接続先が同じネットワークに属しているか、\n"
					+ "またファイアウォールの例外設定がきちんと今のネットワークに行われているか\n"
					+ "を確認してください。",
					"OK"
				);
			}
			else
			{
				await Utils.DisplayAlert("Cannot Open File", "OpenAppLinkAsync Failed\n" + ex.Message, "OK");
			}
			return false;
		}

		ILoader lastLoader = this.Loader;
		this.Loader = loader;
		logger.Info("Loader Initialized");
		lastLoader?.Dispose();
		logger.Debug("Last Loader Disposed");

		// 履歴に追加（HTTPSのURLまたはAppLink）
		string? historyEntry = decodedUrl ?? appLinkString;
		if (historyEntry is not null)
		{
			// pathがListに存在しない場合は、Removeは何も実行されずに終了する
			_ExternalResourceUrlHistory.Remove(historyEntry);
			if (EXTERNAL_RESOURCE_URL_HISTORY_MAX <= _ExternalResourceUrlHistory.Count)
			{
				int removeCount = _ExternalResourceUrlHistory.Count - EXTERNAL_RESOURCE_URL_HISTORY_MAX + 1;
				logger.Debug("ExternalResourceUrlHistory.Count is over EXTERNAL_RESOURCE_URL_HISTORY_MAX ({0} <= {1}) -> remove {2} items", EXTERNAL_RESOURCE_URL_HISTORY_MAX, _ExternalResourceUrlHistory.Count, removeCount);
				_ExternalResourceUrlHistory.RemoveRange(0, removeCount);
			}

			_ExternalResourceUrlHistory.Add(historyEntry);
			AppPreferenceService.SetToJson(AppPreferenceKeys.ExternalResourceUrlHistory, _ExternalResourceUrlHistory, StringListJsonSourceGenerationContext.Default.ListString);
		}

		if (appLinkInfo.RealtimeServiceUri is not null)
		{
			bool doConnect = true;
			if (appLinkInfo.ResourceUri?.Host != appLinkInfo.RealtimeServiceUri.Host)
			{
				doConnect = await Utils.DisplayAlert(
					"External Location Service",
					"位置情報等の取得元が指定されていますが、時刻表ファイルとは別のサーバーが指定されています。"
					+ '\n' +
					"このサーバーを使用してもよろしいですか?"
					+ "Server: " + appLinkInfo.RealtimeServiceUri,
					"はい",
					"いいえ"
				);
				logger.Debug(
					"ResourceUri.Host: {0}, RealtimeServiceUri.Host: {1} -> doConnect: {2}",
					appLinkInfo.ResourceUri?.Host,
					appLinkInfo.RealtimeServiceUri.Host,
					doConnect
				);
			}
			if (doConnect)
			{
				try
				{
					await InstanceManager.LocationService.SetNetworkSyncServiceAsync(appLinkInfo.RealtimeServiceUri, token);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "SetNetworkSyncServiceAsync Failed");
					await Utils.DisplayAlert("Cannot Set External Location Service", "SetNetworkSyncServiceAsync Failed\n" + ex.Message, "OK");
				}
			}
		}

		await Utils.DisplayAlert("Success!", "ファイルの読み込みが完了しました", "OK");
		return true;
	}

	async Task<bool> HandleWebSocketAppLinkAsync(AppLinkInfo appLinkInfo, string? originalAppLink, CancellationToken token)
	{
		if (appLinkInfo.ResourceUri is null)
		{
			logger.Error("ResourceUri is null");
			await Utils.DisplayAlert("Error", "WebSocket URLが指定されていません", "OK");
			return false;
		}

		logger.Info("Connecting to WebSocket: {0}", appLinkInfo.ResourceUri);

		try
		{
			// WebSocketで時刻表データを取得
			OpenFile openFile = new(InstanceManager.HttpClient)
			{
				CanContinueWhenResourceUriContainsIp = CanContinueWhenResourceUriContainsIpHandler,
				CanContinueWhenHeadRequestSuccess = CanContinueWhenHeadRequestSuccessHandler
			};

			WebSocketNetworkSyncService service = await openFile.OpenWebSocketAppLinkAsync(appLinkInfo, token);

			ILoader? lastLoader = this.Loader;
			this.Loader = service;
			logger.Info("Loader Initialized from WebSocket");
			lastLoader?.Dispose();
			logger.Debug("Last Loader Disposed");

			await InstanceManager.LocationService.SetNetworkSyncServiceAsync(service);

			// WebSocketのAppLinkを履歴に追加
			if (originalAppLink is not null)
			{
				_ExternalResourceUrlHistory.Remove(originalAppLink);
				if (EXTERNAL_RESOURCE_URL_HISTORY_MAX <= _ExternalResourceUrlHistory.Count)
				{
					int removeCount = _ExternalResourceUrlHistory.Count - EXTERNAL_RESOURCE_URL_HISTORY_MAX + 1;
					logger.Debug("ExternalResourceUrlHistory.Count is over EXTERNAL_RESOURCE_URL_HISTORY_MAX ({0} <= {1}) -> remove {2} items", EXTERNAL_RESOURCE_URL_HISTORY_MAX, _ExternalResourceUrlHistory.Count, removeCount);
					_ExternalResourceUrlHistory.RemoveRange(0, removeCount);
				}

				_ExternalResourceUrlHistory.Add(originalAppLink);
				AppPreferenceService.SetToJson(AppPreferenceKeys.ExternalResourceUrlHistory, _ExternalResourceUrlHistory, StringListJsonSourceGenerationContext.Default.ListString);
			}

			await Utils.DisplayAlert("Success!", "WebSocket接続が完了しました", "OK");
			return true;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "HandleWebSocketAppLinkAsync Failed");
			if (ex is OperationCanceledException && ex is not TaskCanceledException)
			{
				logger.Debug(ex, "Operation Canceled");
				return false;
			}

			if (appLinkInfo.ResourceUri.HostNameType == UriHostNameType.IPv4
				&& ex is TaskCanceledException
				&& ex.InnerException is TimeoutException)
			{
				logger.Error(ex, "Timeout Error");
				await Utils.DisplayAlert(
					"接続できませんでした (Timeout)",
					"接続先がパソコンの場合は、\n"
					+ "接続先が同じネットワークに属しているか、\n"
					+ "またファイアウォールの例外設定がきちんと今のネットワークに行われているか\n"
					+ "を確認してください。",
					"OK"
				);
			}
			else
			{
				await Utils.DisplayAlert("Cannot Connect WebSocket", "WebSocket接続に失敗しました\n" + ex.Message, "OK");
			}
			return false;
		}
	}

	static async Task<bool> CanContinueWhenResourceUriContainsIpHandler(
		IPAddress remoteIp,
		CancellationToken token
	)
	{
		if (!IsPrivateIpv4(remoteIp))
		{
			logger.Debug(
				"ipAddress: {0} is not private address -> continue",
				remoteIp
			);
			return true;
		}

		bool isSameNetwork = false;
		List<IPAddress> myIpList = [];
		byte[] remoteIpAddress = remoteIp.GetAddressBytes();
		foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
		{
			logger.Trace("adapter: {0} ({1})", adapter.Name, adapter.Description);
			foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
			{
				logger.Trace("  unicastIPAddressInformation: {0} / {1}", unicastIPAddressInformation.Address, unicastIPAddressInformation.IPv4Mask);
				if (unicastIPAddressInformation.Address.AddressFamily != AddressFamily.InterNetwork)
					continue;

				if (!IPAddress.IsLoopback(unicastIPAddressInformation.Address))
					myIpList.Add(unicastIPAddressInformation.Address);
				if (IsSameNetwork(remoteIpAddress, unicastIPAddressInformation.Address, unicastIPAddressInformation.IPv4Mask))
				{
					logger.Trace("    -> isSameNetwork");
					isSameNetwork = true;
					break;
				}
			}

			if (isSameNetwork)
				return true;
		}

		token.ThrowIfCancellationRequested();

		logger.Warn("remoteIp is private but not same network");
		string myIpListStr = string.Join('\n', myIpList.Select(static (x, i) => $"この端末[{i}]:{x}"));
		bool continueProcessing = await Utils.DisplayAlert(
			"Maybe Different Network",
			$"接続先と違うネットワークに属しているため、接続に失敗する可能性があります。\nこのまま接続しますか?\n接続先:{remoteIp}\n{myIpListStr}",
			"続ける",
			"やめる"
		);
		logger.Trace("continueProcessing: {0}", continueProcessing);
		token.ThrowIfCancellationRequested();
		return continueProcessing;
	}

	static async Task<bool> CanContinueWhenHeadRequestSuccessHandler(
		HttpResponseMessage response,
		CancellationToken token
	)
	{
		logger.Info("Head Request status code: {0} ({1})", response.StatusCode);
		if (response.StatusCode == HttpStatusCode.NoContent)
		{
			await Utils.DisplayAlert(
				"Cannot Open File",
				$"時刻表ファイルを確認しましたが、ファイルの中身がありませんでした。",
				"OK"
			);
			return false;
		}

#if DEBUG
		// パフォーマンスとプライバシーの理由で、ヘッダーの内容はDEBUGビルドのみ表示する
		IEnumerable<string> headerStrEnumerable = response.Content.Headers.Select(static x => $"{x.Key}: {string.Join(", ", x.Value)}");
		logger.Trace("ResponseHeaders: {0}", string.Join(", ", headerStrEnumerable));
#endif

		if (response.Content.Headers.ContentLength is not long contentLength)
		{
			logger.Warn("File Size Check Failed (Content-Length not set) -> check continue or not");
			return await Utils.DisplayAlert(
				"Continue to download?",
				"ダウンロードするファイルのサイズが不明です。ダウンロードを継続しますか?",
				"続ける",
				"やめる"
			);
		}

		logger.Info("File Size Check Succeeded: {0} bytes", contentLength);
		return await Utils.DisplayAlert(
			"Continue to download?",
			$"ダウンロードするファイルのサイズは {contentLength} byte です。このファイルをダウンロードしますか?",
			"続ける",
			"やめる"
		);
	}

	static bool IsPrivateIpv4(IPAddress ip)
	{
		if (ip.AddressFamily != AddressFamily.InterNetwork)
		{
			return false;
		}

		byte[] bytes = ip.GetAddressBytes();
		return bytes[0] switch
		{
			10 => true,
			172 => 16 <= bytes[1] && bytes[1] <= 31,
			192 => bytes[1] == 168,
			_ => false,
		};
	}

	static bool IsSameNetwork(byte[] remoteIp, IPAddress localIp, IPAddress subnetMask)
		=> IsSameNetwork(remoteIp, localIp.GetAddressBytes(), subnetMask.GetAddressBytes());
	static bool IsSameNetwork(byte[] remoteIp, byte[] localIp, byte[] subnetMask)
	{
		byte[] remoteNetworkAddress = remoteIp.Select((x, i) => (byte)(x & subnetMask[i])).ToArray();
		byte[] localNetworkAddress = localIp.Select((x, i) => (byte)(x & subnetMask[i])).ToArray();
		return remoteNetworkAddress.SequenceEqual(localNetworkAddress);
	}

}
