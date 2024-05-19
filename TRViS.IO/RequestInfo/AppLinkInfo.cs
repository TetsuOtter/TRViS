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
  Version? RealtimeServiceVersion = null
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
}
