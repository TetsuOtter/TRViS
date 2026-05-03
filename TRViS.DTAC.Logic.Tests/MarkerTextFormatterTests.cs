using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Tests;

public class MarkerTextFormatterTests
{
	// --- LimitMarkerText: null / empty ---

	[Fact]
	public void LimitMarkerText_WithNull_ReturnsNull()
	{
		var result = MarkerTextFormatter.LimitMarkerText(null);
		Assert.Null(result);
	}

	[Fact]
	public void LimitMarkerText_WithEmpty_ReturnsEmpty()
	{
		var result = MarkerTextFormatter.LimitMarkerText("");
		Assert.Equal("", result);
	}

	// --- LimitMarkerText: half-width only ---

	[Fact]
	public void LimitMarkerText_FourHalfWidthChars_ReturnsAsIs()
	{
		// 4 half-width = exactly at limit
		var result = MarkerTextFormatter.LimitMarkerText("ABCD");
		Assert.Equal("ABCD", result);
	}

	[Fact]
	public void LimitMarkerText_FiveHalfWidthChars_TruncatesToFour()
	{
		// 5 half-width > 4 → truncate after 4th
		var result = MarkerTextFormatter.LimitMarkerText("ABCDE");
		Assert.Equal("ABCD", result);
	}

	[Fact]
	public void LimitMarkerText_TwoHalfWidthChars_ReturnsAsIs()
	{
		var result = MarkerTextFormatter.LimitMarkerText("AB");
		Assert.Equal("AB", result);
	}

	// --- LimitMarkerText: full-width (each counts as 2) ---

	[Fact]
	public void LimitMarkerText_TwoFullWidthChars_ReturnsAsIs()
	{
		// "あい" = 2 full-width = 4 half-width equivalent → exactly at limit
		var result = MarkerTextFormatter.LimitMarkerText("あい");
		Assert.Equal("あい", result);
	}

	[Fact]
	public void LimitMarkerText_ThreeFullWidthChars_TruncatesToTwo()
	{
		// "あいう" = 6 half-width equivalent → truncate after 2 full-width
		var result = MarkerTextFormatter.LimitMarkerText("あいう");
		Assert.Equal("あい", result);
	}

	[Fact]
	public void LimitMarkerText_OneFullWidthPlusTwoHalfWidth_ReturnsAsIs()
	{
		// "あAB" = 2+1+1 = 4 → exactly at limit
		var result = MarkerTextFormatter.LimitMarkerText("あAB");
		Assert.Equal("あAB", result);
	}

	[Fact]
	public void LimitMarkerText_OneFullWidthPlusThreeHalfWidth_TruncatesToOneFullTwoHalf()
	{
		// "あABC" = 2+1+1+1 = 5 → truncate after "あAB"
		var result = MarkerTextFormatter.LimitMarkerText("あABC");
		Assert.Equal("あAB", result);
	}

	// --- IsFullWidth ---

	[Fact]
	public void IsFullWidth_WithEmpty_ReturnsFalse()
	{
		Assert.False(MarkerTextFormatter.IsFullWidth(""));
	}

	[Fact]
	public void IsFullWidth_WithHalfWidthAscii_ReturnsFalse()
	{
		Assert.False(MarkerTextFormatter.IsFullWidth("A"));
	}

	[Fact]
	public void IsFullWidth_WithHiragana_ReturnsTrue()
	{
		Assert.True(MarkerTextFormatter.IsFullWidth("あ"));
	}

	[Fact]
	public void IsFullWidth_WithKanji_ReturnsTrue()
	{
		Assert.True(MarkerTextFormatter.IsFullWidth("駅"));
	}
}
