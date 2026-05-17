using TRViS.IO;
using TRViS.Localization;

namespace TRViS.Utils;

public static partial class Util
{
	/// <summary>
	/// Shows the timetable-load failure as a friendly alert (issue #49).
	/// The raw exception detail is intentionally kept out of the dialog —
	/// callers are expected to have already logged it.
	///
	/// <see cref="LoadErrorMessage.Describe"/> only classifies the exception
	/// (it lives in TRViS.IO, which cannot reference the localization
	/// resources); the user-facing strings are produced here via
	/// <see cref="AppResources"/> so the message follows the app language
	/// (issue #40).
	/// </summary>
	public static Task DisplayLoadErrorAsync(Exception ex)
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(ex);
		return DisplayAlertAsync(LoadErrorTitle(info), LoadErrorBody(info), AppResources.Common_OK);
	}

	static string LoadErrorTitle(LoadErrorInfo info)
		=> info.Kind switch
		{
			LoadErrorKind.Timeout => AppResources.LoadError_TimeoutTitle,
			LoadErrorKind.HttpWithStatus or LoadErrorKind.HttpNoStatus
				=> AppResources.LoadError_ConnectionTitle,
			_ => AppResources.LoadError_FileTitle,
		};

	static string LoadErrorBody(LoadErrorInfo info)
		=> info.Kind switch
		{
			LoadErrorKind.JsonMalformed => AppResources.LoadError_JsonMalformedBody
				+ (info.JsonLine is long line
					? string.Format(AppResources.LoadError_JsonPositionFormat, line, info.JsonColumn ?? 0)
					: ""),

			LoadErrorKind.SqliteCorrupt => AppResources.LoadError_SqliteCorruptBody,
			LoadErrorKind.SqliteCannotOpen => AppResources.LoadError_SqliteCannotOpenBody,
			LoadErrorKind.SqlitePermission => AppResources.LoadError_SqlitePermissionBody,
			LoadErrorKind.SqliteIO => AppResources.LoadError_SqliteIOBody,
			LoadErrorKind.SqliteBusy => AppResources.LoadError_SqliteBusyBody,
			LoadErrorKind.SqliteOther => AppResources.LoadError_SqliteOtherBody,

			LoadErrorKind.Timeout => AppResources.LoadError_TimeoutBody,
			LoadErrorKind.FileNotFound => AppResources.LoadError_FileNotFoundBody,
			LoadErrorKind.Unauthorized => AppResources.LoadError_UnauthorizedBody,
			LoadErrorKind.EmptyOrNull => AppResources.LoadError_EmptyOrNullBody,

			LoadErrorKind.HttpWithStatus => string.Format(
				AppResources.LoadError_HttpWithStatusFormat,
				info.HttpStatusCode ?? 0,
				info.HttpStatusName ?? ""),
			LoadErrorKind.HttpNoStatus => AppResources.LoadError_HttpNoStatusBody,

			LoadErrorKind.IOError => AppResources.LoadError_IOBody,

			_ => string.Format(AppResources.LoadError_UnknownFormat, info.RawDetail ?? ""),
		};
}
