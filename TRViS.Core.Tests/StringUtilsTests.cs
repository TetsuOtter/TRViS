namespace TRViS.Core.Tests;

public class StringUtilsTests
{
	[Fact]
	public void ToWide_ConvertsASCIIToWide()
	{
		// Arrange & Act
		var result = StringUtils.ToWide('A');

		// Assert
		Assert.Equal('Ａ', result); // Full-width A
	}

	[Fact]
	public void ToWide_String_ConvertsAllCharacters()
	{
		// Arrange
		var input = "ABC123";

		// Act
		var result = StringUtils.ToWide(input);

		// Assert
		Assert.Equal("ＡＢＣ１２３", result);
	}

	[Fact]
	public void InsertBetweenChars_InsertsString()
	{
		// Arrange
		var input = "ABC".AsSpan();

		// Act
		var result = StringUtils.InsertBetweenChars(input, "-");

		// Assert
		Assert.Equal("A-B-C", result);
	}

	[Fact]
	public void InsertBetweenChars_InsertsChar()
	{
		// Arrange
		var input = "ABC".AsSpan();

		// Act
		var result = StringUtils.InsertBetweenChars(input, '-');

		// Assert
		Assert.Equal("A-B-C", result);
	}

	[Fact]
	public void InsertCharBetweenCharAndMakeWide_InsertsAndConverts()
	{
		// Arrange
		var input = "ABC";

		// Act
		var result = StringUtils.InsertCharBetweenCharAndMakeWide(input, " ");

		// Assert
		Assert.Equal("Ａ Ｂ Ｃ", result); // Wide characters with space between
	}

	[Fact]
	public void InsertBetweenChars_WithEmptyString_ReturnsEmpty()
	{
		// Arrange
		var input = "".AsSpan();

		// Act
		var result = StringUtils.InsertBetweenChars(input, "-");

		// Assert
		Assert.Equal("", result);
	}

	[Fact]
	public void InsertBetweenChars_WithSingleChar_ReturnsChar()
	{
		// Arrange
		var input = "A".AsSpan();

		// Act
		var result = StringUtils.InsertBetweenChars(input, "-");

		// Assert
		Assert.Equal("A", result);
	}
}
