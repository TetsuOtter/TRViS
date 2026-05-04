namespace TRViS.DTAC.Logic.Tests;

public class DestinationFormatterTests
{
	[Fact]
	public void FormatDestination_WithNull_ReturnsNull()
	{
		// Act
		var result = DestinationFormatter.FormatDestination(null);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void FormatDestination_WithEmptyString_ReturnsNull()
	{
		// Act
		var result = DestinationFormatter.FormatDestination("");

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void FormatDestination_WithSingleChar_AddsSpacing()
	{
		// Arrange
		var input = "A";

		// Act
		var result = DestinationFormatter.FormatDestination(input);

		// Assert
		Assert.NotNull(result);
		Assert.Contains("A", result);
		Assert.Contains("行）", result);
		Assert.Contains("（", result);
	}

	[Fact]
	public void FormatDestination_WithTwoChars_AddsSpaceBetween()
	{
		// Arrange
		var input = "AB";

		// Act
		var result = DestinationFormatter.FormatDestination(input);

		// Assert
		Assert.NotNull(result);
		Assert.Contains("A", result);
		Assert.Contains("B", result);
		Assert.Contains("行）", result);
	}

	[Fact]
	public void FormatDestination_WithLongerString_PreservesString()
	{
		// Arrange
		var input = "Tokyo";

		// Act
		var result = DestinationFormatter.FormatDestination(input);

		// Assert
		Assert.NotNull(result);
		Assert.Contains("Tokyo", result);
		Assert.StartsWith("（", result);
		Assert.EndsWith("行）", result);
	}

	[Theory]
	[InlineData("東")]
	[InlineData("上野")]
	[InlineData("新宿駅")]
	public void FormatDestination_WithJapaneseText_FormatsCorrectly(string input)
	{
		// Act
		var result = DestinationFormatter.FormatDestination(input);

		// Assert
		Assert.NotNull(result);
		Assert.Contains(input.Length switch
		{
			1 => $"{DestinationFormatter.SPACE_CHAR}{input}{DestinationFormatter.SPACE_CHAR}",
			2 => $"{input[0]}{DestinationFormatter.SPACE_CHAR}{input[1]}",
			_ => input
		}, result);
	}
}
