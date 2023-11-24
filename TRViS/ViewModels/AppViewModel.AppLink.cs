using System.Collections.Specialized;
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

	const int PATH_LENGTH_MAX = 1024;

	const string OPEN_FILE_JSON = "/open/json";
	const string OPEN_FILE_SQLITE = "/open/sqlite";
	public async Task HandleAppLinkUriAsync(Uri uri, CancellationToken token)
	{
		if (uri.Host != "app")
		{
			logger.Warn("Uri.Host is not `app`: {0}", uri.Host);
			return;
		}
		// if (uri.LocalPath != OPEN_FILE_JSON && uri.LocalPath != OPEN_FILE_SQLITE)
		if (uri.LocalPath != OPEN_FILE_JSON)
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
		if (string.IsNullOrEmpty(path))
		{
			logger.Warn("Uri.Query is not valid (query[`path`] not found): {0}", uri.Query);
			return;
		}
		Uri pathUri = new(path);
		if (pathUri.Scheme != "https" && pathUri.Scheme != "http")
		{
			logger.Warn("path is not valid (not HTTPS nor HTTP): {0}", path);
			return;
		}

		bool openFile = await Utils.DisplayAlert("外部ファイルを開く", $"ファイル `{path}` を開きますか?", "はい", "いいえ");
		logger.Info("Uri: {0} -> openFile: {1}", path, openFile);
		if (!openFile)
		{
			return;
		}

		AppLinkType appLinkType = uri.LocalPath switch
		{
			OPEN_FILE_JSON => AppLinkType.OpenFileJson,
			OPEN_FILE_SQLITE => AppLinkType.OpenFileSQLite,
			_ => AppLinkType.Unknown,
		};

		await LoadExternalFileAsync(path, appLinkType, token);
	}
	public async Task LoadExternalFileAsync(string path, AppLinkType appLinkType, CancellationToken token)
	{
		if (string.IsNullOrEmpty(path))
		{
			logger.Warn("path is null or empty");
			await Utils.DisplayAlert("Cannot Open File", $"File Path is Empty", "OK");
			return;
		}

		string decodedUrl = HttpUtility.UrlDecode(path);
		string encodedUrl = path != decodedUrl ? path : HttpUtility.UrlEncode(path);
		logger.Trace("path: '{0}' -> decodedUrl: '{1}' -> encodedUrl: '{2}'", path, decodedUrl, encodedUrl);

		if (PATH_LENGTH_MAX < decodedUrl.Length)
		{
			logger.Warn("path is too long: {0} < {1}", PATH_LENGTH_MAX, decodedUrl.Length);
			await Utils.DisplayAlert("Cannot Open File", $"File Path is too long: {PATH_LENGTH_MAX} < {decodedUrl.Length}", "OK");
			return;
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
				return;
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
					return;
				}
			}
			else
			{
				logger.Info("File Size Check Succeeded: {0} bytes", contentLength);
				bool downloadContinue = await Utils.DisplayAlert("Continue to download?", $"ダウンロードするファイルのサイズは {contentLength} byte です。このファイルをダウンロードしますか?", "続ける", "やめる");
				if (!downloadContinue)
				{
					logger.Info("User canceled");
					return;
				}
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "File Size Check Failed");
			await Utils.DisplayAlert("Cannot Open File", ex.ToString(), "OK");
			return;
		}

		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, encodedUrl);
			using HttpResponseMessage result = await InstanceManager.HttpClient.SendAsync(request, token);
			if (!result.IsSuccessStatusCode)
			{
				logger.Warn("File Download Failed with status code: {0} ({1})", result.StatusCode, result.Content);
				await Utils.DisplayAlert("Cannot Download File", $"File Download Failed: {result.StatusCode}\n{result.Content}", "OK");
				return;
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
						Uri? uri = new(encodedUrl);
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
							return;
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
					return;
				default:
					logger.Warn("Uri.LocalPath is not valid: {0}", appLinkType);
					return;
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Loading File Failed");
			await Utils.DisplayAlert("Cannot Open File", ex.ToString(), "OK");
			return;
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
	}
}
