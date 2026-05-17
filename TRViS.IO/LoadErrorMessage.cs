using System.Net.Http;
using System.Text.Json;

using SQLite;

namespace TRViS.IO;

/// <summary>
/// User-facing title + body for a timetable-load failure.
/// </summary>
public readonly record struct LoadErrorInfo(string Title, string Body);

/// <summary>
/// Translates the raw library exceptions thrown while loading a timetable
/// (System.Text.Json <see cref="JsonException"/>, sqlite-net
/// <see cref="SQLiteException"/>, file IO, HTTP, timeout) into a short,
/// actionable Japanese message. See issue #49: the raw
/// <c>ex.Message</c> from these libraries is unreadable for end users and
/// too coarse to act on.
///
/// The full technical detail is intentionally NOT put in front of the user
/// — callers already log it (logger.Error + Crashlytics). The raw message is
/// only appended as a last resort when the exception type is unrecognised,
/// where it is the only signal we have.
/// </summary>
public static class LoadErrorMessage
{
	const string FileErrorTitle = "読み込めませんでした";
	const string ConnectionErrorTitle = "接続できませんでした";
	const string TimeoutErrorTitle = "接続できませんでした (Timeout)";

	// Kept verbatim from the previous inline handling in
	// AppViewModel.AppLink.cs so the established timeout guidance text is
	// preserved (it is no longer gated on the host being an IPv4 literal —
	// the advice applies to any connection timeout).
	const string TimeoutBody =
		"接続先がパソコンの場合は、\n"
		+ "接続先が同じネットワークに属しているか、\n"
		+ "またファイアウォールの例外設定がきちんと今のネットワークに行われているか\n"
		+ "を確認してください。";

	public static LoadErrorInfo Describe(Exception ex)
		=> ex switch
		{
			JsonException je => new(
				FileErrorTitle,
				"ファイルのJSON形式が正しくありません。ファイルが壊れていないか、"
					+ "TRViSに対応した時刻表ファイルかを確認してください。"
					+ JsonPositionSuffix(je)),

			SQLiteException se => new(FileErrorTitle, SqliteBody(se)),

			TimeoutException => new(TimeoutErrorTitle, TimeoutBody),
			TaskCanceledException { InnerException: TimeoutException }
				=> new(TimeoutErrorTitle, TimeoutBody),

			FileNotFoundException or DirectoryNotFoundException => new(
				FileErrorTitle,
				"ファイルが見つかりませんでした。ファイルが移動・削除されていないかを確認してください。"),

			UnauthorizedAccessException => new(
				FileErrorTitle,
				"ファイルへのアクセスが拒否されました。ファイルのアクセス権を確認してください。"),

			HttpRequestException he => new(ConnectionErrorTitle, HttpBody(he)),

			// LoaderJson throws ArgumentNullException when the JSON
			// deserialises to null (empty file, or the literal `null`).
			ArgumentNullException => new(
				FileErrorTitle,
				"ファイルの内容が空か、正しい時刻表ファイルではありません。ファイルの内容を確認してください。"),

			// FileNotFound / DirectoryNotFound (more specific) are handled
			// above; this catches the remaining filesystem IO failures.
			IOException => new(
				FileErrorTitle,
				"ファイルの読み込み中にエラーが発生しました。"
					+ "ファイルやストレージに問題がないかを確認してください。"),

			_ => new(
				FileErrorTitle,
				"ファイルの読み込みに失敗しました。\n\n詳細: " + ex.Message),
		};

	static string JsonPositionSuffix(JsonException je)
		=> je.LineNumber is long line
			? $"\n\n(エラー位置: {line + 1}行目, {(je.BytePositionInLine ?? 0) + 1}文字目)"
			: "";

	static string SqliteBody(SQLiteException se)
		=> se.Result switch
		{
			SQLite3.Result.NonDBFile
				or SQLite3.Result.Corrupt
				or SQLite3.Result.Empty
				or SQLite3.Result.Format
				=> "データベースファイルが壊れているか、TRViSに対応した形式ではありません。"
					+ "正しい時刻表データベースファイルかを確認してください。",

			SQLite3.Result.CannotOpen
				=> "データベースファイルを開けませんでした。"
					+ "ファイルが存在し、読み取り可能かを確認してください。",

			SQLite3.Result.Perm
				or SQLite3.Result.AccessDenied
				or SQLite3.Result.ReadOnly
				=> "データベースファイルへのアクセスが拒否されました。"
					+ "ファイルのアクセス権を確認してください。",

			SQLite3.Result.IOError
				=> "データベースファイルの読み込み中にエラーが発生しました。"
					+ "ファイルやストレージに問題がないかを確認してください。",

			SQLite3.Result.Busy or SQLite3.Result.Locked
				=> "データベースファイルが他のアプリで使用中の可能性があります。"
					+ "しばらく待ってから再度お試しください。",

			_ => "データベースファイルの読み込みに失敗しました。"
				+ "正しい時刻表データベースファイルかを確認してください。",
		};

	static string HttpBody(HttpRequestException he)
		=> he.StatusCode is { } code
			? $"サーバーからファイルを取得できませんでした。(サーバー応答コード: {(int)code} {code})"
			: "サーバーからファイルを取得できませんでした。"
				+ "ネットワーク接続と接続先URLを確認してください。";
}
