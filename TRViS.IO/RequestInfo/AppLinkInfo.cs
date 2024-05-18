namespace TRViS.IO.RequestInfo;

public record AppLinkInfo(
  AppLinkInfo.FileType FileTypeInfo,
  AppLinkInfo.CompressionType CompressionTypeInfo,
  AppLinkInfo.EncryptionType EncryptionTypeInfo,
  Uri? ResourceUri,
  byte[]? Content,
  byte[]? DecryptionKey,
  Uri? RealtimeServiceUri,
  string? RealtimeServiceToken,
  string? RealtimeServiceVersion
)
{
  public enum FileType
  {
    Unknown,
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
}
