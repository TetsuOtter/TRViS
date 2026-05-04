namespace TRViS.Core.Tests;

public class Base64UtilsTests
{
	[Fact]
	public void UrlSafeBase64Encode_EncodesString()
	{
		// Arrange
		var input = "Hello World";

		// Act
		var result = Base64Utils.UrlSafeBase64Encode(input);

		// Assert
		Assert.NotNull(result);
		Assert.DoesNotContain('+', result);
		Assert.DoesNotContain('/', result);
		Assert.DoesNotContain('=', result);
	}

	[Fact]
	public void UrlSafeBase64Decode_DecodesString()
	{
		// Arrange
		var input = "Hello World";
		var encoded = Base64Utils.UrlSafeBase64Encode(input);

		// Act
		var decoded = Base64Utils.UrlSafeBase64DecodeToString(encoded);

		// Assert
		Assert.Equal(input, decoded);
	}

	[Fact]
	public void UrlSafeBase64Encode_ByteArray_EncodesCorrectly()
	{
		// Arrange
		var input = new byte[] { 1, 2, 3, 4, 5 };

		// Act
		var result = Base64Utils.UrlSafeBase64Encode(input);

		// Assert
		Assert.NotNull(result);
		Assert.DoesNotContain('+', result);
		Assert.DoesNotContain('/', result);
	}

	[Fact]
	public void UrlSafeBase64Decode_DecodesToByteArray()
	{
		// Arrange
		var input = new byte[] { 1, 2, 3, 4, 5 };
		var encoded = Base64Utils.UrlSafeBase64Encode(input);

		// Act
		var decoded = Base64Utils.UrlSafeBase64Decode(encoded);

		// Assert
		Assert.Equal(input, decoded);
	}

	[Fact]
	public void UrlSafeBase64_RoundTrip_PreservesData()
	{
		// Arrange
		var testStrings = new[]
		{
			"Simple text",
			"Text with special chars: +/=",
			"日本語テキスト", // Japanese text
			"123456789",
			""
		};

		foreach (var testString in testStrings)
		{
			// Act
			var encoded = Base64Utils.UrlSafeBase64Encode(testString);
			var decoded = Base64Utils.UrlSafeBase64DecodeToString(encoded);

			// Assert
			Assert.Equal(testString, decoded);
		}
	}
}
