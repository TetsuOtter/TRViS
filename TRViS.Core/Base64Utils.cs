using System.Text;

namespace TRViS.Core;

/// <summary>
/// Utility methods for URL-safe Base64 encoding/decoding
/// </summary>
public static class Base64Utils
{
	/// <summary>
	/// Encodes a byte array to a URL-safe Base64 string
	/// </summary>
	public static string UrlSafeBase64Encode(byte[] input)
	{
		return Convert.ToBase64String(input)
			.Replace('+', '-')
			.Replace('/', '_')
			.Replace("=", "");
	}

	/// <summary>
	/// Encodes a string to a URL-safe Base64 string
	/// </summary>
	public static string UrlSafeBase64Encode(string input)
	{
		return UrlSafeBase64Encode(Encoding.UTF8.GetBytes(input));
	}

	/// <summary>
	/// Decodes a URL-safe Base64 string to a byte array
	/// </summary>
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

	/// <summary>
	/// Decodes a URL-safe Base64 string to a string
	/// </summary>
	public static string UrlSafeBase64DecodeToString(string input)
	{
		return Encoding.UTF8.GetString(UrlSafeBase64Decode(input));
	}
}
