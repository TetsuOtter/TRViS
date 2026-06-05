using System.Net.Http;
using System.Text.Json;

using SQLite;

namespace TRViS.IO;

/// <summary>
/// Language-independent classification of a timetable-load failure.
///
/// The actual user-facing title/body strings are produced in the app
/// (presentation) layer where the localization resources live —
/// <see cref="LoadErrorMessage"/> deliberately stays free of any
/// <c>AppResources</c> reference because <c>TRViS.IO</c> cannot reference
/// the MAUI app project (the dependency goes the other way). See issue #40
/// (i18n): the previous hardcoded-Japanese messages were not localizable.
/// </summary>
public enum LoadErrorKind
{
	/// <summary>System.Text.Json could not parse the file.</summary>
	JsonMalformed,

	/// <summary>SQLite: non-DB / corrupt / empty / format.</summary>
	SqliteCorrupt,

	/// <summary>SQLite: cannot open.</summary>
	SqliteCannotOpen,

	/// <summary>SQLite: permission / access denied / read-only.</summary>
	SqlitePermission,

	/// <summary>SQLite: IO error.</summary>
	SqliteIO,

	/// <summary>SQLite: busy / locked (in use by another app).</summary>
	SqliteBusy,

	/// <summary>SQLite: any other result code.</summary>
	SqliteOther,

	/// <summary>Connection timed out.</summary>
	Timeout,

	/// <summary>File / directory not found.</summary>
	FileNotFound,

	/// <summary>File access denied.</summary>
	Unauthorized,

	/// <summary>File empty or deserialised to null.</summary>
	EmptyOrNull,

	/// <summary>HTTP request failed with a status code.</summary>
	HttpWithStatus,

	/// <summary>HTTP request failed without a status code.</summary>
	HttpNoStatus,

	/// <summary>Generic filesystem IO failure.</summary>
	IOError,

	/// <summary>Unrecognised exception — raw detail is the only signal.</summary>
	Unknown,
}

/// <summary>
/// Classified timetable-load failure. <see cref="Kind"/> selects the
/// localized title/body; the nullable fields carry the parameters needed to
/// fill the format strings for that kind.
/// </summary>
public readonly record struct LoadErrorInfo(
	LoadErrorKind Kind,
	long? JsonLine = null,
	long? JsonColumn = null,
	int? HttpStatusCode = null,
	string? HttpStatusName = null,
	string? RawDetail = null);

/// <summary>
/// Translates the raw library exceptions thrown while loading a timetable
/// (System.Text.Json <see cref="JsonException"/>, sqlite-net
/// <see cref="SQLiteException"/>, file IO, HTTP, timeout) into a
/// language-independent <see cref="LoadErrorInfo"/>. See issue #49: the raw
/// <c>ex.Message</c> from these libraries is unreadable for end users and
/// too coarse to act on; issue #40: the message must be localizable, so the
/// string production moved to the app layer.
///
/// The full technical detail is intentionally NOT put in front of the user
/// — callers already log it (logger.Error + Crashlytics). The raw message is
/// only carried (<see cref="LoadErrorInfo.RawDetail"/>) as a last resort
/// when the exception type is unrecognised, where it is the only signal we
/// have.
/// </summary>
public static class LoadErrorMessage
{
	public static LoadErrorInfo Describe(Exception ex)
		=> ex switch
		{
			JsonException je => je.LineNumber is long line
				? new(LoadErrorKind.JsonMalformed,
					JsonLine: line + 1,
					JsonColumn: (je.BytePositionInLine ?? 0) + 1)
				: new(LoadErrorKind.JsonMalformed),

			SQLiteException se => new(SqliteKind(se)),

			TimeoutException => new(LoadErrorKind.Timeout),
			TaskCanceledException { InnerException: TimeoutException }
				=> new(LoadErrorKind.Timeout),

			FileNotFoundException or DirectoryNotFoundException
				=> new(LoadErrorKind.FileNotFound),

			UnauthorizedAccessException => new(LoadErrorKind.Unauthorized),

			HttpRequestException he => he.StatusCode is { } code
				? new(LoadErrorKind.HttpWithStatus,
					HttpStatusCode: (int)code,
					HttpStatusName: code.ToString())
				: new(LoadErrorKind.HttpNoStatus),

			// LoaderJson throws ArgumentNullException when the JSON
			// deserialises to null (empty file, or the literal `null`).
			ArgumentNullException => new(LoadErrorKind.EmptyOrNull),

			// FileNotFound / DirectoryNotFound (more specific) are handled
			// above; this catches the remaining filesystem IO failures.
			IOException => new(LoadErrorKind.IOError),

			_ => new(LoadErrorKind.Unknown, RawDetail: ex.Message),
		};

	static LoadErrorKind SqliteKind(SQLiteException se)
		=> se.Result switch
		{
			SQLite3.Result.NonDBFile
				or SQLite3.Result.Corrupt
				or SQLite3.Result.Empty
				or SQLite3.Result.Format
				=> LoadErrorKind.SqliteCorrupt,

			SQLite3.Result.CannotOpen => LoadErrorKind.SqliteCannotOpen,

			SQLite3.Result.Perm
				or SQLite3.Result.AccessDenied
				or SQLite3.Result.ReadOnly
				=> LoadErrorKind.SqlitePermission,

			SQLite3.Result.IOError => LoadErrorKind.SqliteIO,

			SQLite3.Result.Busy or SQLite3.Result.Locked
				=> LoadErrorKind.SqliteBusy,

			_ => LoadErrorKind.SqliteOther,
		};
}
