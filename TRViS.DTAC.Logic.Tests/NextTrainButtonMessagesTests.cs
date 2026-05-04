using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Tests;

public class NextTrainButtonMessagesTests
{
	// --- FormatButtonText ---

	[Fact]
	public void FormatButtonText_AsciiInput_HasSuffix()
	{
		string result = NextTrainButtonMessages.FormatButtonText("123A");
		Assert.EndsWith(NextTrainButtonMessages.ButtonTextSuffix, result);
	}

	[Fact]
	public void FormatButtonText_SingleChar_NoThinSpacePrefixed()
	{
		// Single character should have no thin space inserted before it
		string result = NextTrainButtonMessages.FormatButtonText("A");
		Assert.True(result.StartsWith("Ａ"), $"Expected to start with full-width A, got: {result}");
	}

	[Fact]
	public void FormatButtonText_MultiChar_ContainsThinSpace()
	{
		string result = NextTrainButtonMessages.FormatButtonText("AB");
		Assert.Contains(TRViS.Core.StringUtils.THIN_SPACE, result);
	}

	[Fact]
	public void FormatButtonText_Empty_HasSuffix()
	{
		string result = NextTrainButtonMessages.FormatButtonText(string.Empty);
		Assert.Equal(NextTrainButtonMessages.ButtonTextSuffix, result);
	}

	// --- FormatSetterErrorMessage ---

	[Fact]
	public void FormatSetterErrorMessage_ContainsAllFields()
	{
		string result = NextTrainButtonMessages.FormatSetterErrorMessage(
			workGroupId: "WG1",
			workId: "W1",
			trainId: "T1",
			currentNextTrainId: "current",
			givenNextTrainId: "given");

		Assert.Contains("WG1", result);
		Assert.Contains("W1", result);
		Assert.Contains("T1", result);
		Assert.Contains("current", result);
		Assert.Contains("given", result);
	}

	[Fact]
	public void FormatSetterErrorMessage_NullFields_DoesNotThrow()
	{
		string result = NextTrainButtonMessages.FormatSetterErrorMessage(
			workGroupId: null,
			workId: null,
			trainId: null,
			currentNextTrainId: string.Empty,
			givenNextTrainId: null);

		Assert.NotNull(result);
		Assert.Contains("Cannot get the timetable", result);
	}

	// --- FormatClickErrorMessage ---

	[Fact]
	public void FormatClickErrorMessage_ContainsAllFields()
	{
		string result = NextTrainButtonMessages.FormatClickErrorMessage(
			workGroupId: "WG2",
			workId: "W2",
			trainId: "T2",
			nextTrainId: "NEXT");

		Assert.Contains("WG2", result);
		Assert.Contains("W2", result);
		Assert.Contains("T2", result);
		Assert.Contains("NEXT", result);
	}

	[Fact]
	public void FormatClickErrorMessage_NullFields_DoesNotThrow()
	{
		string result = NextTrainButtonMessages.FormatClickErrorMessage(
			workGroupId: null,
			workId: null,
			trainId: null,
			nextTrainId: "X");

		Assert.NotNull(result);
		Assert.Contains("次の列車", result);
	}
}
