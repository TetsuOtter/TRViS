using System.Collections.Specialized;
using System.Web;

namespace TRViS.IO.RequestInfo;

public record AppLinkInfo(
  AppLinkInfo.FileType FileTypeInfo,
  Version Version,
  AppLinkInfo.CompressionType CompressionTypeInfo = AppLinkInfo.CompressionType.None,
  AppLinkInfo.EncryptionType EncryptionTypeInfo = AppLinkInfo.EncryptionType.None,
  Uri? ResourceUri = null,
  byte[]? Content = null,
  byte[]? DecryptionKey = null,
  Uri? RealtimeServiceUri = null,
  string? RealtimeServiceToken = null,
  Version? RealtimeServiceVersion = null,
  string? LocalPath = null
)
{
  public enum FileType
  {
    Sqlite,
    Json,
  };

  public enum CompressionType
  {
    None,
    Gzip,
  };

  public enum EncryptionType
  {
    None,
  };

	static readonly Version supportedMaxVersion = new(1, 0);
	const string OPEN_FILE_JSON = "/open/json";
	const string OPEN_FILE_SQLITE = "/open/sqlite";

  public static AppLinkInfo FromAppLink(
    string appLink
  )
    => AppLinkInfo.FromAppLink(new Uri(appLink));

  public static AppLinkInfo FromAppLink(
    Uri uri
  )
  {
    		// Scheme部分はチェックしない
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
		string? localPathQuery = queryParams["local"];
		string? decryptionKeyQuery = queryParams["key"];
		if (encryptionType != AppLinkInfo.EncryptionType.None && string.IsNullOrEmpty(decryptionKeyQuery))
		{
			throw new ArgumentException("DecryptionKey is required when EncryptionType is not None");
		}

		if (string.IsNullOrEmpty(resourceUriQuery)
			&& string.IsNullOrEmpty(dataQuery)
			&& string.IsNullOrEmpty(localPathQuery))
		{
			throw new ArgumentException("At least one of ResourceUri, Data, or LocalPath must be set");
		}

		string? localPath = null;
		if (!string.IsNullOrEmpty(localPathQuery))
		{
			localPath = ValidateLocalPath(localPathQuery);
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
			realtimeServiceVersion,
			localPath
		);
  }

	// Syntactic checks for the `local` query value. The semantic check (resolving
	// against the app's TimetableFileDirectory and verifying the result stays
	// within it) is the caller's responsibility — this project doesn't know that
	// directory. We reject anything that's obviously trying to escape: absolute
	// paths, drive letters, backslashes, NUL/invalid chars, and `..` segments.
	static string ValidateLocalPath(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException("local path is empty");

		// Reject backslashes outright — we standardize on '/' as the segment
		// separator so the semantic check on the caller side has one shape to
		// reason about. Windows accepts '/' anyway.
		if (value.IndexOf('\\') >= 0)
			throw new ArgumentException("local path must not contain backslashes");

		// Reject anything that begins like an absolute path: leading slash,
		// or a Windows drive letter (`C:` etc.).
		if (value[0] == '/')
			throw new ArgumentException("local path must be relative");
		if (value.Length >= 2 && value[1] == ':')
			throw new ArgumentException("local path must be relative");

		string[] segments = value.Split('/');
		char[] invalid = Path.GetInvalidFileNameChars();
		foreach (string segment in segments)
		{
			if (segment.Length == 0)
				throw new ArgumentException("local path must not contain empty segments");
			if (segment == "..")
				throw new ArgumentException("local path must not contain `..` segments");
			if (segment == ".")
				throw new ArgumentException("local path must not contain `.` segments");
			if (segment.IndexOfAny(invalid) >= 0)
				throw new ArgumentException("local path contains invalid characters");
		}

		return value;
	}
}
