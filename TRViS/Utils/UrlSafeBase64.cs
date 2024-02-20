using System.Text;

namespace TRViS;

public static partial class Utils
{
  public static string UrlSafeBase64Encode(byte[] input)
  {
    return Convert.ToBase64String(input)
      .Replace('+', '-')
      .Replace('/', '_')
      .Replace("=", "");
  }
  public static string UrlSafeBase64Encode(string input)
  {
    return UrlSafeBase64Encode(Encoding.UTF8.GetBytes(input));
  }

  public static byte[] UrlSafeBase64Decode(string input)
  {
    string incoming = input.Replace('-', '+').Replace('_', '/');
    switch (input.Length % 4)
    {
      case 2:
        incoming += "==";
        break;
      case 3:
        incoming += "=";
        break;
    }
    return Convert.FromBase64String(incoming);
  }

  public static string UrlSafeBase64DecodeToString(string input)
  {
    return Encoding.UTF8.GetString(UrlSafeBase64Decode(input));
  }
}
