namespace TRViS.Core.Tests;

public class NotificationSoundDecoderTests
{
	[Fact]
	public void TryDecode_ValidBase64_ReturnsBytes()
	{
		byte[] expected = [1, 2, 3, 4];
		string base64 = Convert.ToBase64String(expected);

		byte[]? result = NotificationSoundDecoder.TryDecode(base64);

		Assert.Equal(expected, result);
	}

	[Fact]
	public void TryDecode_DataUriPrefix_StripsPrefixBeforeDecoding()
	{
		byte[] expected = [5, 6, 7];
		string base64 = "data:audio/wav;base64," + Convert.ToBase64String(expected);

		byte[]? result = NotificationSoundDecoder.TryDecode(base64);

		Assert.Equal(expected, result);
	}

	[Fact]
	public void TryDecode_MalformedBase64_ReturnsNull()
	{
		byte[]? result = NotificationSoundDecoder.TryDecode("not-valid-base64!!!");

		Assert.Null(result);
	}

	[Fact]
	public void TryDecode_ExactlyAtLimit_ReturnsBytes()
	{
		byte[] data = new byte[NotificationSoundDecoder.MaxDecodedBytes];
		string base64 = Convert.ToBase64String(data);

		byte[]? result = NotificationSoundDecoder.TryDecode(base64);

		Assert.NotNull(result);
		Assert.Equal(data.Length, result!.Length);
	}

	[Fact]
	public void TryDecode_OneByteOverLimit_ReturnsNull()
	{
		byte[] data = new byte[NotificationSoundDecoder.MaxDecodedBytes + 1];
		string base64 = Convert.ToBase64String(data);

		byte[]? result = NotificationSoundDecoder.TryDecode(base64);

		Assert.Null(result);
	}
}
