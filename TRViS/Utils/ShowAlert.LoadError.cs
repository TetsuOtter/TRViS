using TRViS.IO;

namespace TRViS.Utils;

public static partial class Util
{
	/// <summary>
	/// Shows the timetable-load failure as a friendly alert (issue #49).
	/// The raw exception detail is intentionally kept out of the dialog —
	/// callers are expected to have already logged it.
	/// </summary>
	public static Task DisplayLoadErrorAsync(Exception ex)
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(ex);
		return DisplayAlertAsync(info.Title, info.Body, "OK");
	}
}
