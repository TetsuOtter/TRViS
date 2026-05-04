using TRViS.Core;

namespace TRViS.DTAC.Logic.Formatters;

/// <summary>
/// Pure helper for formatting NextTrainButton display text and error messages.
/// No UI or InstanceManager dependencies — fully unit-testable.
/// </summary>
internal static class NextTrainButtonMessages
{
	/// <summary>
	/// The suffix appended to the formatted train number on the button label.
	/// </summary>
	public const string ButtonTextSuffix = "の時刻表へ";

	/// <summary>
	/// Formats the button label text for the given train number.
	/// Converts each ASCII character to its full-width equivalent and inserts thin spaces between characters.
	/// </summary>
	/// <param name="trainNumber">Raw train number string (e.g. "123A").</param>
	/// <returns>Formatted button label text.</returns>
	public static string FormatButtonText(string trainNumber)
	{
		string wide = StringUtils.InsertCharBetweenCharAndMakeWide(trainNumber, StringUtils.THIN_SPACE);
		return wide + ButtonTextSuffix;
	}

	/// <summary>
	/// Formats the error message for failures when setting the next train ID (property setter).
	/// </summary>
	public static string FormatSetterErrorMessage(
		string? workGroupId,
		string? workId,
		string? trainId,
		string currentNextTrainId,
		string? givenNextTrainId)
	{
		return "Cannot get the timetable of the next train.\n"
			+ $"WorkGroupID: {workGroupId}\n"
			+ $"WorkID: {workId}\n"
			+ $"TrainID: {trainId}\n"
			+ $"CurrentNextTrainID: {currentNextTrainId}\n"
			+ $"GivenNextTrainID: {givenNextTrainId}";
	}

	/// <summary>
	/// Formats the error message for failures when the button is clicked.
	/// </summary>
	public static string FormatClickErrorMessage(
		string? workGroupId,
		string? workId,
		string? trainId,
		string nextTrainId)
	{
		return "次の列車の時刻表を取得できませんでした。\n"
			+ $"WorkGroupID: {workGroupId}\n"
			+ $"WorkID: {workId}\n"
			+ $"TrainID: {trainId}\n"
			+ $"NextTrainID: {nextTrainId}";
	}
}
