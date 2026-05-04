using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Tests;

public class DriveTimeFormatterTests
{
	// --- FormatMinutes ---

	[Fact]
	public void FormatMinutes_WithNull_ReturnsNull()
	{
		var result = DriveTimeFormatter.FormatMinutes(null);
		Assert.Null(result);
	}

	[Fact]
	public void FormatMinutes_WithEmpty_ReturnsEmpty()
	{
		var result = DriveTimeFormatter.FormatMinutes("");
		Assert.Equal("", result);
	}

	[Fact]
	public void FormatMinutes_OneChar_ReturnsAsIs()
	{
		var result = DriveTimeFormatter.FormatMinutes("5");
		Assert.Equal("5", result);
	}

	[Fact]
	public void FormatMinutes_TwoChars_ReturnsAsIs()
	{
		var result = DriveTimeFormatter.FormatMinutes("59");
		Assert.Equal("59", result);
	}

	[Fact]
	public void FormatMinutes_ThreeChars_ReturnsAsterisks()
	{
		// 3+ characters is an overflow → show "**"
		var result = DriveTimeFormatter.FormatMinutes("100");
		Assert.Equal("**", result);
	}

	[Fact]
	public void FormatMinutes_FourChars_ReturnsAsterisks()
	{
		var result = DriveTimeFormatter.FormatMinutes("1234");
		Assert.Equal("**", result);
	}

	// --- FormatSeconds ---

	[Fact]
	public void FormatSeconds_WithNull_ReturnsNull()
	{
		var result = DriveTimeFormatter.FormatSeconds(null);
		Assert.Null(result);
	}

	[Fact]
	public void FormatSeconds_WithEmpty_ReturnsEmpty()
	{
		var result = DriveTimeFormatter.FormatSeconds("");
		Assert.Equal("", result);
	}

	[Fact]
	public void FormatSeconds_OneChar_PrependsTwoSpaces()
	{
		// Single-character second value gets two leading spaces for alignment
		var result = DriveTimeFormatter.FormatSeconds("5");
		Assert.Equal("  5", result);
	}

	[Fact]
	public void FormatSeconds_TwoChars_ReturnsAsIs()
	{
		var result = DriveTimeFormatter.FormatSeconds("30");
		Assert.Equal("30", result);
	}

	[Fact]
	public void FormatSeconds_TwoCharsZeroPrefixed_ReturnsAsIs()
	{
		var result = DriveTimeFormatter.FormatSeconds("05");
		Assert.Equal("05", result);
	}

	[Fact]
	public void FormatSeconds_ThreeOrMoreChars_ReturnsAsIs()
	{
		var result = DriveTimeFormatter.FormatSeconds("123");
		Assert.Equal("123", result);
	}
}
