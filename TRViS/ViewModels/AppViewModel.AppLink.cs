using System.Collections.Specialized;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Web;

using TRViS.IO;
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

	internal const int PATH_LENGTH_MAX = 1024;

	const string OPEN_FILE_JSON = "/open/json";
	const string OPEN_FILE_SQLITE = "/open/sqlite";
	public async Task HandleAppLinkUriAsync(Uri uri, CancellationToken token)
	{
		if (uri.Host != "app")
		{
			logger.Warn("Uri.Host is not `app`: {0}", uri.Host);
			return;
		}
		AppLinkType appLinkType = uri.LocalPath switch
		{
			OPEN_FILE_JSON => AppLinkType.OpenFileJson,
			OPEN_FILE_SQLITE => AppLinkType.OpenFileSQLite,
			_ => AppLinkType.Unknown,
		};
		// JSONのみ実装済み
		if (appLinkType == AppLinkType.Unknown || appLinkType != AppLinkType.OpenFileJson)
		{
			logger.Warn("Uri.LocalPath is not valid: {0}", uri.LocalPath);
			return;
		}
		if (string.IsNullOrEmpty(uri.Query))
		{
			logger.Warn("Uri.Query is null or empty");
			return;
		}
		NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);
		string? path = queryParams["path"];
		if (!string.IsNullOrEmpty(path))
		{
			await LoadExternalFileFromUrlAsync(uri.Query, appLinkType, token);
			return;
		}

		string? data_UrlSafeBase64Str = queryParams["data"];
		if (!string.IsNullOrEmpty(data_UrlSafeBase64Str))
		{
			await LoadExternalFileFromDataAsync(data_UrlSafeBase64Str, appLinkType, token);
			return;
		}
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

	public async Task<bool> LoadExternalFileFromUrlAsync(string path, AppLinkType appLinkType, CancellationToken token)
	{
		if (string.IsNullOrEmpty(path))
		{
			logger.Warn("Uri.Query is not valid (query[`path`] not found): {0}", path);
			await Utils.DisplayAlert("Cannot Open File", $"URL is Empty", "OK");
			return false;
		}

		Uri pathUri;
		try
		{
			pathUri = new(path);
		}
		catch (UriFormatException ex)
		{
			logger.Error(ex, "UriFormatException");
			await Utils.DisplayAlert("Cannot Open File", ex.Message, "OK");
			return false;
		}

		if (pathUri.Scheme != "https" && pathUri.Scheme != "http")
		{
			logger.Warn("path is not valid (not HTTPS nor HTTP): {0}", path);
			return false;
		}

		bool openFile = await Utils.DisplayAlert("外部ファイルを開く", $"ファイル `{path}` を開きますか?", "はい", "いいえ");
		logger.Info("Uri: {0} -> openFile: {1}", path, openFile);
		if (!openFile)
		{
			return false;
		}

		return await LoadExternalFileAsync(path, appLinkType, token);
	}

	public async Task<bool> LoadExternalFileFromDataAsync(string urlSafeBase64Str, AppLinkType appLinkType, CancellationToken token)
	{
		if (string.IsNullOrEmpty(urlSafeBase64Str))
		{
			logger.Warn("Uri.Query is not valid (query[`data`] not found): {0}", urlSafeBase64Str);
			await Utils.DisplayAlert("Cannot Open File", $"Data is Empty", "OK");
			return false;
		}

		byte[] dataBytes;
		try
		{
			dataBytes = Utils.UrlSafeBase64Decode(urlSafeBase64Str);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "UrlSafeBase64Decode Failed");
			await Utils.DisplayAlert("Cannot Load File", ex.Message, "OK");
			return false;
		}

		if (dataBytes.Length == 0)
		{
			logger.Warn("dataBytes.Length is 0");
			await Utils.DisplayAlert("Cannot Load File", $"Data is Empty", "OK");
			return false;
		}

		try
		{
			switch (appLinkType)
			{
				case AppLinkType.OpenFileJson:
					Loader = LoaderJson.InitFromBytes(dataBytes);
					return true;
				case AppLinkType.OpenFileSQLite:
					logger.Error("Not Implemented");
					await Utils.DisplayAlert("Not Implemented", "Open External SQLite file is Not Implemented", "OK");
					return false;
				default:
					logger.Warn("Uri.LocalPath is not valid: {0}", appLinkType);
					return false;
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Cannot load file");
			await Utils.DisplayAlert("Cannot Load File", ex.Message, "OK");
			return false;
		}
	}

	public async Task<bool> LoadExternalFileAsync(string path, AppLinkType appLinkType, CancellationToken token)
	{
		if (string.IsNullOrEmpty(path))
		{
			logger.Warn("path is null or empty");
			await Utils.DisplayAlert("Cannot Open File", $"File Path is Empty", "OK");
			return false;
		}

		string decodedUrl = HttpUtility.UrlDecode(path);
		string encodedUrl;
		if (path != decodedUrl)
		{
			logger.Trace("path: '{0}' -> decodedUrl: '{1}'", path, decodedUrl);
			encodedUrl = path;
		}
		else
		{
			#pragma warning disable SYSLIB0013
			encodedUrl = Uri.EscapeUriString(path);
			#pragma warning restore SYSLIB0013
			logger.Trace("path: '{0}' -> encodedUrl: '{1}'", path, encodedUrl);
		}

		if (PATH_LENGTH_MAX < decodedUrl.Length)
		{
			logger.Warn("path is too long: {0} < {1}", PATH_LENGTH_MAX, decodedUrl.Length);
			await Utils.DisplayAlert("Cannot Open File", $"File Path is too long: {PATH_LENGTH_MAX} < {decodedUrl.Length}", "OK");
			return false;
		}

		Uri uri = new(encodedUrl);
		IPAddress? remoteIp = null;
		bool isRemoteIpv4Ip = uri.HostNameType == UriHostNameType.IPv4
			&& IPAddress.TryParse(uri.Host, out remoteIp);
		bool isRemoteIpv4PrivateIp = isRemoteIpv4Ip && IsPrivateIpv4(remoteIp!);
		logger.Trace("uri.HostNameType: {0}, uri.Host: {1}, isRemoteIpv4Ip: {2}, isRemoteIpv4PrivateIp: {3}", uri.HostNameType, uri.Host, isRemoteIpv4Ip, isRemoteIpv4PrivateIp);
		if (isRemoteIpv4PrivateIp)
		{
			bool isSameNetwork = false;
			List<IPAddress> myIpList = [];
			byte[] remoteIpAddress = remoteIp!.GetAddressBytes();
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
					break;
			}

			if (!isSameNetwork)
			{
				logger.Warn("remoteIp is private but not same network");
				string myIpListStr = string.Join('\n', myIpList.Select((x, i) => $"この端末[{i}]:{x}"));
				bool continueProcessing = await Utils.DisplayAlert(
					"Maybe Different Network",
					$"接続先と違うネットワークに属しているため、接続に失敗する可能性があります。\nこのまま接続しますか?\n接続先:{remoteIp}\n{myIpListStr}",
					"続ける",
					"やめる"
				);
				logger.Trace("continueProcessing: {0}", continueProcessing);
				if (!continueProcessing)
					return false;
			}
		}

		try
		{
			logger.Info("checking file size and type...");
			using HttpRequestMessage request = new(HttpMethod.Head, encodedUrl);
			using HttpResponseMessage checkResult = await InstanceManager.HttpClient.SendAsync(request, token);
			if (!checkResult.IsSuccessStatusCode)
			{
				logger.Warn("File Size Check Failed with status code: {0} ({1})", checkResult.StatusCode, checkResult.Content);
				await Utils.DisplayAlert("Cannot Open File", $"File Size Check Failed: {checkResult.StatusCode}\n{checkResult.Content}", "OK");
				return false;
			}

#if DEBUG
			// パフォーマンスとプライバシーの理由で、ヘッダーの内容はDEBUGビルドのみ表示する
			IEnumerable<string> headerStrs = checkResult.Content.Headers.Select(x => $"{x.Key}: {string.Join(", ", x.Value)}");
			logger.Trace("ResponseHeaders: {0}", string.Join(", ", headerStrs));
#endif

			if (checkResult.Content.Headers.ContentLength is not long contentLength)
			{
				logger.Warn("File Size Check Failed (Content-Length not set) -> check continue or not");
				bool downloadContinue = await Utils.DisplayAlert("Continue to download?", "ダウンロードするファイルのサイズが不明です。ダウンロードを継続しますか?", "続ける", "やめる");
				if (!downloadContinue)
				{
					logger.Info("User canceled");
					return false;
				}
			}
			else
			{
				logger.Info("File Size Check Succeeded: {0} bytes", contentLength);
				bool downloadContinue = await Utils.DisplayAlert("Continue to download?", $"ダウンロードするファイルのサイズは {contentLength} byte です。このファイルをダウンロードしますか?", "続ける", "やめる");
				if (!downloadContinue)
				{
					logger.Info("User canceled");
					return false;
				}
			}
		}
		catch (Exception ex)
		{
			if (isRemoteIpv4PrivateIp
				&& ex is TaskCanceledException
				&& ex.InnerException is TimeoutException)
			{
				logger.Error(ex, "File Size Check Failed (ToLocal && Timeout)");
				await Utils.DisplayAlert(
					"接続できませんでした",
					"接続先が同じネットワークに属しているか、\nまたファイアウォールの例外設定がきちんと今のネットワークに行われているかを\n確認してください。",
					"OK");
				return false;
			}
			logger.Error(ex, "File Size Check Failed");
			await Utils.DisplayAlert("Cannot Open File", ex.Message, "OK");
			return false;
		}

		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, encodedUrl);
			using HttpResponseMessage result = await InstanceManager.HttpClient.SendAsync(request, token);
			if (!result.IsSuccessStatusCode)
			{
				logger.Warn("File Download Failed with status code: {0} ({1})", result.StatusCode, result.Content);
				await Utils.DisplayAlert("Cannot Download File", $"File Download Failed: {result.StatusCode}\n{result.Content}", "OK");
				return false;
			}

			if (appLinkType == AppLinkType.Unknown)
			{
				switch (result.Content.Headers.ContentType?.MediaType)
				{
					case "application/json":
						appLinkType = AppLinkType.OpenFileJson;
						break;
					case "application/x-sqlite3":
						appLinkType = AppLinkType.OpenFileSQLite;
						break;
					default:
						string? lastPathSegment = uri.Segments.LastOrDefault();
						if (lastPathSegment?.EndsWith(".json") == true)
						{
							logger.Info("File Type is not valid, but file extension is `.json` -> OpenFileJson");
							appLinkType = AppLinkType.OpenFileJson;
						}
						else if (lastPathSegment?.EndsWith(".sqlite") == true)
						{
							logger.Info("File Type is not valid, but file extension is `.sqlite` -> OpenFileSQLite");
							appLinkType = AppLinkType.OpenFileSQLite;
						}
						else
						{
							logger.Warn("File Type is not valid: {0}", result.Content.Headers.ContentType?.MediaType);
							await Utils.DisplayAlert("Cannot Open File", $"File Type is not valid: {result.Content.Headers.ContentType?.MediaType}", "OK");
							return false;
						}
						break;
				}
			}

			using Stream stream = result.Content.ReadAsStream(token);

			ILoader? lastLoader = Loader;
			switch (appLinkType)
			{
				case AppLinkType.OpenFileJson:
					logger.Debug("Loading JSON File");
					Loader = await LoaderJson.InitFromStreamAsync(stream, token);
					lastLoader?.Dispose();
					logger.Trace("LoaderJson Initialized");
					break;
				case AppLinkType.OpenFileSQLite:
					logger.Debug("Loading SQLite File");
					// 一旦ローカルに保存してから読み込む
					logger.Error("Not Implemented");
					await Utils.DisplayAlert("Not Implemented", "Open External SQLite file is Not Implemented", "OK");
					logger.Trace("LoaderSQL Initialized");
					return false;
				default:
					logger.Warn("Uri.LocalPath is not valid: {0}", appLinkType);
					return false;
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Loading File Failed");
			await Utils.DisplayAlert("Cannot Open File", ex.Message, "OK");
			return false;
		}

		// pathがListに存在しない場合は、Removeは何も実行されずに終了する
		_ExternalResourceUrlHistory.Remove(decodedUrl);
		if (EXTERNAL_RESOURCE_URL_HISTORY_MAX <= _ExternalResourceUrlHistory.Count)
		{
			int removeCount = _ExternalResourceUrlHistory.Count - EXTERNAL_RESOURCE_URL_HISTORY_MAX + 1;
			logger.Debug("ExternalResourceUrlHistory.Count is over EXTERNAL_RESOURCE_URL_HISTORY_MAX ({0} <= {1}) -> remove {2} items", EXTERNAL_RESOURCE_URL_HISTORY_MAX, _ExternalResourceUrlHistory.Count, removeCount);
			_ExternalResourceUrlHistory.RemoveRange(0, removeCount);
		}

		_ExternalResourceUrlHistory.Add(decodedUrl);
		AppPreferenceService.SetToJson(AppPreferenceKeys.ExternalResourceUrlHistory, _ExternalResourceUrlHistory);
		await Utils.DisplayAlert("Success!", "外部ファイルの読み込みが完了しました", "OK");
		return true;
	}
}
