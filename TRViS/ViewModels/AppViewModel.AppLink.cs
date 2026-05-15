using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Web;

using TRViS.IO;
using TRViS.IO.RequestInfo;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Utils;

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

	public Task<bool> HandleAppLinkUriAsync(string uri, CancellationToken token)
		=> HandleAppLinkUriAsync(uri, addToHistory: true, token);

	/// <summary>
	/// <paramref name="addToHistory"/> controls whether a successful load is
	/// added to <see cref="ExternalResourceUrlHistory"/>. Default <c>true</c>
	/// preserves OS-deeplink / App.xaml.cs entry points; the in-app
	/// "Connect to Server" dialog passes <c>false</c> when the user
	/// unticks "接続先を保存する".
	/// </summary>
	public async Task<bool> HandleAppLinkUriAsync(string uri, bool addToHistory, CancellationToken token)
	{
#if UI_TEST
		// Test-only: seed the URL history list so UI tests can exercise the
		// "tap a history item" flow without standing up a real HTTP server.
		// Format: trvis://_test/seed-url-history?urls=<url1>|<url2>|...
		// The "|" separator avoids URL-encoding ambiguity with comma in URIs.
		// Guarded by #if UI_TEST so this only ships in CI test builds.
		const string TestSeedHistoryPrefix = "trvis://_test/seed-url-history";
		if (uri.StartsWith(TestSeedHistoryPrefix, StringComparison.OrdinalIgnoreCase))
		{
			HandleTestSeedUrlHistory(uri);
			return true;
		}

		// Test-only: push a GPS coord into LocationService so UI tests can
		// exercise the GPS-driven auto-scroll path without CoreLocation/permissions.
		// Format: trvis://_test/set-gps-location?lon=<num>&lat=<num>[&acc=<num>]
		const string TestSetGpsLocationPrefix = "trvis://_test/set-gps-location";
		if (uri.StartsWith(TestSetGpsLocationPrefix, StringComparison.OrdinalIgnoreCase))
		{
			HandleTestSetGpsLocation(uri);
			return true;
		}
#endif

		AppLinkInfo appLinkInfo;
		try
		{
			appLinkInfo = AppLinkInfo.FromAppLink(uri);
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "AppLinkInfo Identify Failed");
			await Util.DisplayAlertAsync("Cannot Open File", "AppLinkInfo Identify Failed\n" + ex.Message, "OK");
			return false;
		}

		token.ThrowIfCancellationRequested();

		if (appLinkInfo.ResourceUri is not null && appLinkInfo.ResourceUri.Scheme is "http" or "https")
		{
			string path = appLinkInfo.ResourceUri.ToString();
			string decodedUrl = HttpUtility.UrlDecode(path);

			bool openRemoteFileCheckResult = await Util.DisplayAlertAsync(
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

		return await HandleAppLinkUriAsync(appLinkInfo, uri, addToHistory, token);
	}
	public Task<bool> HandleAppLinkUriAsync(AppLinkInfo appLinkInfo, CancellationToken token)
		=> HandleAppLinkUriAsync(appLinkInfo, addToHistory: true, token);
	public Task<bool> HandleAppLinkUriAsync(AppLinkInfo appLinkInfo, bool addToHistory, CancellationToken token)
		=> HandleAppLinkUriAsync(appLinkInfo, null, addToHistory, token);

	/// <summary>
	/// <paramref name="originalAppLink"/> is the raw URL string the user/system supplied
	/// before it was parsed into <see cref="AppLinkInfo"/>. The WebSocket branch only adds
	/// to <see cref="ExternalResourceUrlHistory"/> when this is non-null, because the
	/// constructed <see cref="AppLinkInfo"/> does not retain the originating URL form.
	/// Callers that build an <see cref="AppLinkInfo"/> directly (e.g. ConnectServerDialog
	/// for raw <c>ws://</c> / <c>wss://</c> entries) must pass the original text here so
	/// history persistence works for the WebSocket path.
	/// </summary>
	public async Task<bool> HandleAppLinkUriAsync(AppLinkInfo appLinkInfo, string? originalAppLink, bool addToHistory, CancellationToken token)
	{
		string? decodedUrl = null;
		string? appLinkString = originalAppLink;

		// `local=` AppLink: file lives inside the app's TimetableFileDirectory.
		// Resolve to an absolute file path here (where we know the directory),
		// reject anything that escapes it, then rewrite to a file:// ResourceUri
		// so the rest of the pipeline treats it like a normal local file.
		// No privacy-policy gate or confirmation prompt: the user explicitly
		// invoked this AppLink for a file already on their device.
		if (appLinkInfo.LocalPath is not null)
		{
			if (!TryResolveLocalTimetablePath(appLinkInfo.LocalPath, out string? resolvedPath, out string? errorMessage))
			{
				logger.Warn("LocalPath rejected: {0} (input: {1})", errorMessage, appLinkInfo.LocalPath);
				await Util.DisplayAlertAsync("Cannot Open File", errorMessage ?? "LocalPath is invalid", "OK");
				return false;
			}
			appLinkInfo = appLinkInfo with { ResourceUri = new Uri(resolvedPath!), LocalPath = null };
		}

		if (appLinkInfo.ResourceUri is not null && appLinkInfo.ResourceUri.Scheme is "http" or "https")
		{
			decodedUrl = HttpUtility.UrlDecode(appLinkInfo.ResourceUri.ToString());
		}

		token.ThrowIfCancellationRequested();

		// ResourceUriがWebSocket（ws:// or wss://）の場合、直接NetworkSyncServiceに接続
		if (appLinkInfo.ResourceUri is not null && appLinkInfo.ResourceUri.Scheme is "ws" or "wss")
		{
			logger.Info("ResourceUri is WebSocket -> Connect to NetworkSyncService directly");
			return await HandleWebSocketAppLinkAsync(appLinkInfo, appLinkString, addToHistory, token);
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
		catch (OperationCanceledException)
		{
			logger.Debug("OpenAppLinkAsync was cancelled");
			return false;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "OpenAppLinkAsync Failed");
			if (appLinkInfo.ResourceUri?.HostNameType == UriHostNameType.IPv4
				&& ex is TaskCanceledException
				&& ex.InnerException is TimeoutException)
			{
				logger.Error(ex, "Timeout Error");
				await Util.DisplayAlertAsync(
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
				await Util.DisplayAlertAsync("Cannot Open File", "OpenAppLinkAsync Failed\n" + ex.Message, "OK");
			}
			return false;
		}

		ILoader? lastLoader = this.Loader;
		this.SetLoader(loader, decodedUrl ?? appLinkInfo.ResourceUri?.ToString() ?? appLinkString);
		logger.Info("Loader Initialized");
		lastLoader?.Dispose();
		logger.Debug("Last Loader Disposed");

		// 履歴に追加（HTTPSのURLまたはAppLink）
		string? historyEntry = addToHistory ? (decodedUrl ?? appLinkString) : null;
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
				doConnect = await Util.DisplayAlertAsync(
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
					await Util.DisplayAlertAsync("Cannot Set External Location Service", "SetNetworkSyncServiceAsync Failed\n" + ex.Message, "OK");
				}
			}
		}

		await Util.DisplayAlertAsync("Success!", "ファイルの読み込みが完了しました", "OK");
		return true;
	}

	async Task<bool> HandleWebSocketAppLinkAsync(AppLinkInfo appLinkInfo, string? originalAppLink, bool addToHistory, CancellationToken token)
	{
		if (appLinkInfo.ResourceUri is null)
		{
			logger.Error("ResourceUri is null");
			await Util.DisplayAlertAsync("Error", "WebSocket URLが指定されていません", "OK");
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
			this.SetLoader(service, originalAppLink ?? appLinkInfo.ResourceUri?.ToString());
			logger.Info("Loader Initialized from WebSocket");
			lastLoader?.Dispose();
			logger.Debug("Last Loader Disposed");

			InstanceManager.LocationService.SetNetworkSyncService(service);

			// WebSocketのAppLinkを履歴に追加
			if (addToHistory && originalAppLink is not null)
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

			await Util.DisplayAlertAsync("Success!", "WebSocket接続が完了しました", "OK");
			return true;
		}
		catch (OperationCanceledException)
		{
			logger.Debug("HandleWebSocketAppLinkAsync was cancelled");
			return false;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "HandleWebSocketAppLinkAsync Failed");

			if (appLinkInfo.ResourceUri.HostNameType == UriHostNameType.IPv4
				&& ex is TaskCanceledException
				&& ex.InnerException is TimeoutException)
			{
				logger.Error(ex, "Timeout Error");
				await Util.DisplayAlertAsync(
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
				await Util.DisplayAlertAsync("Cannot Connect WebSocket", "WebSocket接続に失敗しました\n" + ex.Message, "OK");
			}
			return false;
		}
	}

	/// <summary>
	/// Resolve a `local=` AppLink path against <see cref="DirectoryPathProvider.TimetableFileDirectory"/>.
	/// AppLinkInfo has already done the syntactic checks (no `..`, `/`, `\`,
	/// drive letters, invalid filename chars). This is the *semantic* check —
	/// it canonicalises the path and verifies the result still lives under the
	/// expected base directory. (Symlinks would bypass this; the app does not
	/// create symlinks in TimetableFileDirectory so we accept that gap.)
	/// </summary>
	private static bool TryResolveLocalTimetablePath(string localPath, out string? resolvedPath, out string? errorMessage)
	{
		resolvedPath = null;
		errorMessage = null;
		try
		{
			string baseDir = Path.GetFullPath(DirectoryPathProvider.TimetableFileDirectory.FullName);
			string baseDirWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar)
				? baseDir
				: baseDir + Path.DirectorySeparatorChar;

			string candidate = Path.GetFullPath(Path.Combine(baseDir, localPath));
			if (!candidate.StartsWith(baseDirWithSep, StringComparison.Ordinal))
			{
				errorMessage = "指定されたファイルは時刻表フォルダの外にあります。";
				return false;
			}

			if (!File.Exists(candidate))
			{
				errorMessage = $"ファイルが見つかりません: {localPath}";
				return false;
			}

			resolvedPath = candidate;
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = $"ファイルパスの解決に失敗しました: {ex.Message}";
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
		bool continueProcessing = await Util.DisplayAlertAsync(
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
			await Util.DisplayAlertAsync(
				"Cannot Open File",
				"サーバーには接続できましたが、時刻表データがまだありません。\n"
				+ "連携元のソフト(ゲーム等)でシナリオ・列車を読み込んでから、もう一度お試しください。",
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
			return await Util.DisplayAlertAsync(
				"Continue to download?",
				"ダウンロードするファイルのサイズが不明です。ダウンロードを継続しますか?",
				"続ける",
				"やめる"
			);
		}

		logger.Info("File Size Check Succeeded: {0} bytes", contentLength);
		return await Util.DisplayAlertAsync(
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

#if UI_TEST
	/// <summary>
	/// Test-only seed for ExternalResourceUrlHistory. Invoked when a UI test
	/// passes a "trvis://_test/seed-url-history?urls=a|b|c" deeplink through
	/// the LoadFromWeb popup. Adds the URLs to history and persists, mimicking
	/// what HandleAppLinkUriAsync does on a successful load.
	/// </summary>
	private void HandleTestSeedUrlHistory(string uri)
	{
		logger.Info("Test seed URL history invoked: {0}", uri);

		int qIndex = uri.IndexOf('?');
		if (qIndex < 0)
		{
			logger.Warn("Test seed URL history: no query string");
			return;
		}

		var query = HttpUtility.ParseQueryString(uri.Substring(qIndex + 1));
		string? urlsRaw = query["urls"];
		if (string.IsNullOrEmpty(urlsRaw))
		{
			logger.Warn("Test seed URL history: 'urls' parameter missing");
			return;
		}

		// "|" separator chosen because it does not require percent-encoding
		// inside a query value and won't conflict with URL chars in entries.
		string[] urls = urlsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries);
		foreach (string url in urls)
		{
			_ExternalResourceUrlHistory.Remove(url);
			_ExternalResourceUrlHistory.Add(url);
		}
		AppPreferenceService.SetToJson(
			AppPreferenceKeys.ExternalResourceUrlHistory,
			_ExternalResourceUrlHistory,
			StringListJsonSourceGenerationContext.Default.ListString);

		logger.Info("Test seed URL history: persisted {0} URLs", urls.Length);
	}

	/// <summary>
	/// Public test-only seed for ExternalResourceUrlHistory. Called from the
	/// StartHomePage's hidden test seed button; lets UI tests bypass typing
	/// through Appium SendKeys (which is flaky on iOS XCUITest for long URLs).
	/// </summary>
	public void SeedUrlHistoryForTesting(IEnumerable<string> urls)
	{
		foreach (string url in urls)
		{
			if (string.IsNullOrWhiteSpace(url))
				continue;
			_ExternalResourceUrlHistory.Remove(url);
			_ExternalResourceUrlHistory.Add(url);
		}
		AppPreferenceService.SetToJson(
			AppPreferenceKeys.ExternalResourceUrlHistory,
			_ExternalResourceUrlHistory,
			StringListJsonSourceGenerationContext.Default.ListString);
		logger.Info("SeedUrlHistoryForTesting: persisted {0} URLs", _ExternalResourceUrlHistory.Count);
	}

	/// <summary>
	/// Public test-only clear for ExternalResourceUrlHistory. Lets the
	/// "empty history" code path tests start from a known-clean state without
	/// relying on per-session filesystem resets — on iOS, simctl-level
	/// preference deletion has been observed to race with the app's in-memory
	/// list when noReset:true is set.
	/// </summary>
	public void ClearUrlHistoryForTesting()
	{
		_ExternalResourceUrlHistory.Clear();
		AppPreferenceService.SetToJson(
			AppPreferenceKeys.ExternalResourceUrlHistory,
			_ExternalResourceUrlHistory,
			StringListJsonSourceGenerationContext.Default.ListString);
		logger.Info("ClearUrlHistoryForTesting: cleared in-memory + persisted history");
	}

	/// <summary>
	/// Test-only: push a GPS coord into LocationService.SetGpsLocation. Used by
	/// the UI test that exercises GPS-driven auto-scroll without CoreLocation
	/// or runtime permission prompts.
	/// </summary>
	private void HandleTestSetGpsLocation(string uri)
	{
		logger.Info("Test set GPS location invoked: {0}", uri);

		int qIndex = uri.IndexOf('?');
		if (qIndex < 0)
		{
			logger.Warn("Test set GPS location: no query string");
			return;
		}

		var query = HttpUtility.ParseQueryString(uri.Substring(qIndex + 1));
		string? lonStr = query["lon"];
		string? latStr = query["lat"];
		if (!double.TryParse(lonStr, System.Globalization.CultureInfo.InvariantCulture, out double lon)
			|| !double.TryParse(latStr, System.Globalization.CultureInfo.InvariantCulture, out double lat))
		{
			logger.Warn("Test set GPS location: invalid lon/lat ('{0}'/'{1}')", lonStr, latStr);
			return;
		}

		double? acc = null;
		if (double.TryParse(query["acc"], System.Globalization.CultureInfo.InvariantCulture, out double parsedAcc))
			acc = parsedAcc;

		// Initialize the LonLatLocationService first so SetGpsLocation has a
		// _CurrentService to dispatch to. Do NOT toggle IsEnabled — on iOS that
		// triggers LocationServiceGpsAdapter.StartListening which prompts the
		// system CoreLocation permission alert and stalls the test. The
		// OnGpsLocationUpdated event still fires at the top of SetGpsLocation
		// before the IsEnabled gate would early-return.
		var locationService = InstanceManager.LocationService;
		locationService.SetLonLatLocationService();
		locationService.SetGpsLocation(lon, lat, acc, useAverageDistance: false);
		logger.Info("Test set GPS location: dispatched (lon={0}, lat={1}, acc={2})", lon, lat, acc);
	}
#endif

}
