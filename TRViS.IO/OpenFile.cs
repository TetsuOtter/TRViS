using System.Collections.Specialized;
using System.Web;
using TRViS.IO.RequestInfo;

namespace TRViS.IO;

public static class OpenFile
{
	static readonly Version supportedMaxVersion = new(1, 0);
	// public static Task<ILoader> OpenAppLinkAsync(
	// 	string appLink,
	// 	CancellationToken token
	// )
	// {
	// 	AppLinkInfo appLinkInfo = IdentifyAppLinkInfo(appLink);
	// 	return OpenAppLinkAsync(appLinkInfo, token);
	// }

	// public static Task<ILoader> OpenAppLinkAsync(
	// 	AppLinkInfo appLinkInfo,
	// 	CancellationToken token
	// )
	// {
	// }

	const string OPEN_FILE_JSON = "/open/json";
	const string OPEN_FILE_SQLITE = "/open/sqlite";
	public static AppLinkInfo IdentifyAppLinkInfo(
		string appLink
	)
	{
		// Scheme部分はチェックしない
		Uri uri = new(appLink);
		if (uri.Host != "app")
		{
			throw new ArgumentException("host is not `app`");
		}

		string path = uri.LocalPath;
		AppLinkInfo.FileType? fileType = path switch
		{
			OPEN_FILE_JSON => AppLinkInfo.FileType.Json,
			OPEN_FILE_SQLITE => AppLinkInfo.FileType.Sqlite,
			_ => null,
		};
		if (fileType is null)
		{
			throw new ArgumentException("Unknown file type");
		}

		if (string.IsNullOrEmpty(uri.Query))
		{
			throw new ArgumentException("Query is empty");
		}

		NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);
		string? versionQuery = queryParams["ver"];
		Version version = string.IsNullOrEmpty(versionQuery) ? new(1,0) : new(versionQuery);
		if (supportedMaxVersion < version) {
			throw new ArgumentException("Unsupported version");
		}

		AppLinkInfo.CompressionType compressionType = queryParams["cmp"] switch
		{
			null or "" or "none" => AppLinkInfo.CompressionType.None,
			"gzip" => AppLinkInfo.CompressionType.Gzip,
			_ => throw new ArgumentException("Unknown compression type"),
		};

		AppLinkInfo.EncryptionType encryptionType = queryParams["enc"] switch
		{
			null or "" or "none" => AppLinkInfo.EncryptionType.None,
			_ => throw new ArgumentException("Unknown encryption type"),
		};

		string? resourceUriQuery = queryParams["path"];
		string? dataQuery = queryParams["data"];
		string? decryptionKeyQuery = queryParams["key"];
		if (encryptionType != AppLinkInfo.EncryptionType.None && string.IsNullOrEmpty(decryptionKeyQuery))
		{
			throw new ArgumentException("DecryptionKey is required when EncryptionType is not None");
		}

		if (string.IsNullOrEmpty(resourceUriQuery) && string.IsNullOrEmpty(dataQuery))
		{
			throw new ArgumentException("At least one of ResourceUri or Data must be set");
		}

		string? realtimeServiceUriQuery = queryParams["rts"];
		string? realtimeServiceToken = queryParams["rtk"];
		string? realtimeServiceVersionQuery = queryParams["rtv"];
		Version? realtimeServiceVersion = string.IsNullOrEmpty(realtimeServiceVersionQuery) ? null : new(realtimeServiceVersionQuery);

		Uri? resourceUri = string.IsNullOrEmpty(resourceUriQuery) ? null : new Uri(resourceUriQuery);
		byte[]? content = string.IsNullOrEmpty(dataQuery) ? null : Utils.UrlSafeBase64Decode(dataQuery);
		byte[]? decryptionKey = string.IsNullOrEmpty(decryptionKeyQuery) ? null : Utils.UrlSafeBase64Decode(decryptionKeyQuery);
		Uri? realtimeServiceUri = string.IsNullOrEmpty(realtimeServiceUriQuery) ? null : new Uri(realtimeServiceUriQuery);

		return new AppLinkInfo(
			fileType.Value,
			version,
			compressionType,
			encryptionType,
			resourceUri,
			content,
			decryptionKey,
			realtimeServiceUri,
			realtimeServiceToken,
			realtimeServiceVersion
		);
	}
}
