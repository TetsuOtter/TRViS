namespace TRViS.Core;

/// <summary>
/// <see cref="SoundRef"/> の Base64 デコードとサイズ上限判定 (16MiB, #329) を行う
/// MAUI 非依存の純粋関数。再生を担う <c>NotificationSoundPlayer</c> から呼ばれる。
/// </summary>
public static class NotificationSoundDecoder
{
	/// <summary>デコード後の音声バイナリの上限 (16MiB)。特別な意味は無く、
	/// 無制限のバイナリ再生を避けるための実務上の目安 (#329)。</summary>
	public const int MaxDecodedBytes = 16 * 1024 * 1024;

	/// <summary>
	/// <paramref name="base64"/> (data URI プレフィックス <c>data:audio/...;base64,</c> を
	/// 含んでいてもよい) をデコードする。不正な Base64、またはデコード後のサイズが
	/// <see cref="MaxDecodedBytes"/> を超える場合は <c>null</c> を返す (例外を投げない)。
	/// </summary>
	public static byte[]? TryDecode(string base64)
	{
		int commaIndex = base64.IndexOf(',');
		string payload = base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0
			? base64[(commaIndex + 1)..]
			: base64;

		byte[] bytes;
		try
		{
			bytes = Convert.FromBase64String(payload);
		}
		catch (FormatException)
		{
			return null;
		}

		return bytes.Length > MaxDecodedBytes ? null : bytes;
	}
}
