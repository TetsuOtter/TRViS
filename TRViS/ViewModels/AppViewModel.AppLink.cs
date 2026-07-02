using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Web;

using TRViS.IO;
using TRViS.IO.RequestInfo;
using TRViS.Localization;
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

	// --- WebSocket connection-lost tracking / reconnect (#261) ---
	// Captured at the last successful WebSocket connect so the Home screen's
	// 再接続 button can re-run the exact same connect. The AppLinkInfo is kept
	// (not just LoaderSourceLabel) because AppLinkInfo.FromAppLink rejects a raw
	// ws:// string (it requires host == "app"), so the display label alone is
	// not reconnectable.
	private AppLinkInfo? _lastWebSocketAppLinkInfo;
	private string? _lastWebSocketOriginalAppLink;
	// Watches the active WS service for ConnectionClosed / ConnectionFailed and
	// detaches the old one on reconnect. The subscribe/resubscribe/sender-guard
	// logic is extracted here so it can be unit-tested without MAUI; the only
	// MAUI-bound part (MainThread marshaling + observable set) stays in the
	// MarkServerConnectionLost callback below.
	private NetworkSyncConnectionLostWatcher? _wsConnectionLostWatcherField;
	private NetworkSyncConnectionLostWatcher WsConnectionLostWatcher
		=> _wsConnectionLostWatcherField ??= new NetworkSyncConnectionLostWatcher(
			MarkServerConnectionLost,
			MarkServerReconnecting,
			MarkServerReconnected);

	/// <summary>
	/// 切断イベント監視を解除し、再接続情報を破棄する。WebSocket 以外 / null の
	/// ローダーに切り替わったとき (<see cref="OnLoaderChanged"/>) に呼ばれる。
	/// </summary>
	internal void ClearWebSocketConnectionTracking()
	{
		WsConnectionLostWatcher.Clear();
		_lastWebSocketAppLinkInfo = null;
		_lastWebSocketOriginalAppLink = null;
	}

	// ConnectionClosed / ConnectionFailed は WebSocket の受信ループスレッドから
	// 発火する。ここでは Loader を差し替えず (切断後もキャッシュ済みデータを
	// Home 画面に出し続けたいため。LocationService 側が GPS へフォールバックして
	// サービスを Dispose 済みでもキャッシュは読める)、フラグだけ立てる。
	// 観測対象プロパティの set は HomeGridView のラベル / 表示更新を駆動するので
	// UI スレッドへマーシャリングする。
	private void MarkServerConnectionLost()
	{
		logger.Info("WebSocket connection lost -> IsServerConnectionLost = true");
		RunOnMainThread(() =>
		{
			// 再接続試行が終わった (クリーンクローズ or 再接続失敗) 状態。
			// IsServerReconnecting も落として Connecting で固着しないようにする (#266)。
			IsServerReconnecting = false;
			IsServerConnectionLost = true;
		});
	}

	// 自動再接続の開始 (#266): ぐるぐる表示へ。Loader / IsServerConnectionLost は
	// そのまま (再接続成功までキャッシュ表示を継続)。ServerConnectionStatus の
	// 算出で IsServerReconnecting が優先されるため Connecting になる。
	private void MarkServerReconnecting()
	{
		logger.Info("WebSocket reconnecting -> IsServerReconnecting = true");
		RunOnMainThread(() => IsServerReconnecting = true);
	}

	// 自動再接続の成功 (#266): 接続済みへ。#261 の「自動再接続成功後も
	// IsServerConnectionLost が true のまま固着する」ギャップもここで解消する
	// (Home の切断バナーも自動復帰するようになる)。
	private void MarkServerReconnected()
	{
		logger.Info("WebSocket reconnected -> clear reconnecting/lost flags");
		RunOnMainThread(() =>
		{
			IsServerReconnecting = false;
			IsServerConnectionLost = false;
		});
	}

	private static void RunOnMainThread(Action action)
	{
		if (MainThread.IsMainThread)
			action();
		else
			MainThread.BeginInvokeOnMainThread(action);
	}

	/// <summary>
	/// 直近に成功した WebSocket 接続と同じ接続先へ再接続する。Home 画面の
	/// 再接続ボタンから呼ばれる。再接続情報が無い場合は何もせず false を返す。
	/// </summary>
	public Task<bool> ReconnectWebSocketAsync(CancellationToken token)
	{
		AppLinkInfo? info = _lastWebSocketAppLinkInfo;
		if (info is null)
		{
			logger.Warn("ReconnectWebSocketAsync: no stored WebSocket AppLink to reconnect with");
			return Task.FromResult(false);
		}
		logger.Info("ReconnectWebSocketAsync: reconnecting to {0}", info.ResourceUri);
		// addToHistory: false — a reconnect is not a new user-initiated entry.
		return HandleWebSocketAppLinkAsync(info, _lastWebSocketOriginalAppLink, addToHistory: false, token);
	}

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
			await Util.DisplayAlertAsync(AppResources.AppLink_CannotOpenFileTitle, string.Format(AppResources.AppLink_IdentifyFailedFormat, ex.Message), AppResources.Common_OK);
			return false;
		}

		token.ThrowIfCancellationRequested();

		if (appLinkInfo.ResourceUri is not null && appLinkInfo.ResourceUri.Scheme is "http" or "https")
		{
			string path = appLinkInfo.ResourceUri.ToString();
			string decodedUrl = HttpUtility.UrlDecode(path);

			bool openRemoteFileCheckResult = await Util.DisplayAlertAsync(
				AppResources.AppLink_OpenExternalFileTitle,
				string.Format(AppResources.AppLink_OpenExternalFileFormat, decodedUrl),
				AppResources.Common_Yes,
				AppResources.Common_No
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
				await Util.DisplayAlertAsync(AppResources.AppLink_CannotOpenFileTitle, errorMessage ?? AppResources.AppLink_LocalPathInvalid, AppResources.Common_OK);
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
			// main 側 (#49) の DisplayLoadErrorAsync は TimeoutException /
			// TaskCanceledException を分類し、#40 の IPv4 タイムアウト特例を
			// 意図的に一般化した上位互換 (LoadErrorMessage 参照)。重複を避け
			// こちらに統一する。LoadErrorMessage の多言語化は本 i18n の範囲外。
			await Util.DisplayLoadErrorAsync(ex);
			return false;
		}

		ILoader? lastLoader = this.Loader;
		this.SetLoader(loader, decodedUrl ?? appLinkInfo.ResourceUri?.ToString() ?? appLinkString);
		logger.Info("Loader Initialized");
		lastLoader?.Dispose();
		logger.Debug("Last Loader Disposed");

		// HTTP(S) integration (TRViS.LocalServers): the user explicitly pointed the
		// app at a server to show its timetable, so skip the Home picker entirely.
		// SelectionManager.OnLoaderChanged already auto-committed when there was a
		// single WorkGroup; force the first one here for the multi-WorkGroup case
		// so a timetable is always ready, then ask the UI to jump to it.
		if (appLinkInfo.ResourceUri?.Scheme is "http" or "https")
		{
			if (SelectionManager.SelectedWorkGroup is null)
				SelectionManager.SelectedWorkGroup = SelectionManager.WorkGroupList?.FirstOrDefault();
			RequestAutoNavigateToTimetable();
		}

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
					AppResources.AppLink_ExternalLocationServiceTitle,
					string.Format(AppResources.AppLink_ExternalLocationServiceBodyFormat, appLinkInfo.RealtimeServiceUri),
					AppResources.Common_Yes,
					AppResources.Common_No
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
					await Util.DisplayAlertAsync(AppResources.AppLink_CannotSetExternalLocationTitle, string.Format(AppResources.AppLink_SetNetworkSyncFailedFormat, ex.Message), AppResources.Common_OK);
				}
			}
		}

		await Util.DisplayAlertAsync(AppResources.Common_Success, AppResources.AppLink_FileLoadCompleteBody, AppResources.Common_OK);
		return true;
	}

	internal async Task<bool> AutoConnectWebSocketAsync(string appLinkUri, CancellationToken token)
	{
		AppLinkInfo appLinkInfo;
		try
		{
			appLinkInfo = AppLinkInfo.FromAppLink(appLinkUri);
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "AutoConnectWebSocket: AppLinkInfo parse failed for {0}", appLinkUri);
			return false;
		}

		if (appLinkInfo.ResourceUri?.Scheme is not ("ws" or "wss"))
		{
			logger.Warn("AutoConnectWebSocket: not a WebSocket URI");
			return false;
		}

		return await HandleWebSocketAppLinkAsync(appLinkInfo, appLinkUri, addToHistory: false, token, showSuccessAlert: false);
	}

	async Task<bool> HandleWebSocketAppLinkAsync(AppLinkInfo appLinkInfo, string? originalAppLink, bool addToHistory, CancellationToken token, bool showSuccessAlert = true)
	{
		if (appLinkInfo.ResourceUri is null)
		{
			logger.Error("ResourceUri is null");
			await Util.DisplayAlertAsync(AppResources.Common_Error, AppResources.AppLink_WebSocketUrlMissing, AppResources.Common_OK);
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

			// Remember how to reconnect, watch this service for disconnects, and
			// clear any prior "connection lost" banner — we now have a fresh live
			// connection. Subscribe before SetLoader so an immediate drop is caught.
			_lastWebSocketAppLinkInfo = appLinkInfo;
			_lastWebSocketOriginalAppLink = originalAppLink;
			WsConnectionLostWatcher.Watch(service);
			IsServerConnectionLost = false;
			IsServerReconnecting = false;

			ILoader? lastLoader = this.Loader;
			this.SetLoader(service, originalAppLink ?? appLinkInfo.ResourceUri?.ToString());
			logger.Info("Loader Initialized from WebSocket");
			lastLoader?.Dispose();
			logger.Debug("Last Loader Disposed");

			InstanceManager.LocationService.SetNetworkSyncService(service);

			// WebSocket is a server-driven integration: jump straight to the
			// timetable. WorkGroup data may still be arriving via server push, so
			// the selection is best-effort here (SelectionManager.OnLoaderChanged
			// already auto-committed if a single WorkGroup was present); the
			// server's SelectTrain push refines it afterwards regardless.
			if (SelectionManager.SelectedWorkGroup is null)
				SelectionManager.SelectedWorkGroup = SelectionManager.WorkGroupList?.FirstOrDefault();
			RequestAutoNavigateToTimetable();

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

			if (showSuccessAlert)
				await Util.DisplayAlertAsync(AppResources.Common_Success, AppResources.AppLink_WebSocketConnectedBody, AppResources.Common_OK);
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
			// main 側 (#49) の DisplayLoadErrorAsync に統一 (上記と同様、
			// タイムアウト特例を含め LoadErrorMessage が上位互換)。
			await Util.DisplayLoadErrorAsync(ex);
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
				errorMessage = AppResources.AppLink_LocalFileOutsideFolder;
				return false;
			}

			if (!File.Exists(candidate))
			{
				errorMessage = string.Format(AppResources.AppLink_FileNotFoundFormat, localPath);
				return false;
			}

			resolvedPath = candidate;
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = string.Format(AppResources.AppLink_PathResolveFailedFormat, ex.Message);
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
		string myIpListStr = string.Join('\n', myIpList.Select(static (x, i) => string.Format(AppResources.AppLink_ThisDeviceFormat, i, x)));
		bool continueProcessing = await Util.DisplayAlertAsync(
			AppResources.AppLink_MaybeDifferentNetworkTitle,
			string.Format(AppResources.AppLink_MaybeDifferentNetworkBodyFormat, remoteIp, myIpListStr),
			AppResources.Common_Continue,
			AppResources.Common_Stop
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
				AppResources.AppLink_CannotOpenFileTitle,
				AppResources.AppLink_EmptyFileBody,
				AppResources.Common_OK
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
				AppResources.AppLink_ContinueDownloadTitle,
				AppResources.AppLink_UnknownSizeBody,
				AppResources.Common_Continue,
				AppResources.Common_Stop
			);
		}

		logger.Info("File Size Check Succeeded: {0} bytes", contentLength);
		return await Util.DisplayAlertAsync(
			AppResources.AppLink_ContinueDownloadTitle,
			string.Format(AppResources.AppLink_FileSizeFormat, contentLength),
			AppResources.Common_Continue,
			AppResources.Common_Stop
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
