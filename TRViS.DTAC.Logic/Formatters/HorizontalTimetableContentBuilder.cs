using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Formatters;

/// <summary>
/// Discriminated result describing how the horizontal timetable should be rendered.
/// </summary>
public enum HorizontalTimetableRenderKind
{
	None,
	Html,
	Uri,
}

/// <summary>
/// 横型時刻表 (PNG/JPG/PDF/URI) を表示用の文字列に変換した結果。
/// View 層は WebView.Source を <see cref="Kind"/> によって決定する。
/// </summary>
public readonly record struct HorizontalTimetableRenderResult(
	HorizontalTimetableRenderKind Kind,
	string Payload)
{
	public static HorizontalTimetableRenderResult None { get; } = new(HorizontalTimetableRenderKind.None, string.Empty);
	public static HorizontalTimetableRenderResult Html(string html) => new(HorizontalTimetableRenderKind.Html, html);
	public static HorizontalTimetableRenderResult Uri(string uri) => new(HorizontalTimetableRenderKind.Uri, uri);
}

/// <summary>
/// 横型時刻表表示の純粋ロジック。
/// View からは Build() の結果を WebView に流し込むだけ。
/// </summary>
public static class HorizontalTimetableContentBuilder
{
	/// <summary>
	/// Work が横型時刻表を持つか (= ボタン表示可否)。
	/// </summary>
	public static bool HasHorizontalTimetable(Work? work)
		=> work?.HasETrainTimetable == true && work.ETrainTimetableContent is not null;

	/// <summary>
	/// Work から WebView に流す表示内容を組み立てる。
	/// 内容を持たない場合は <see cref="HorizontalTimetableRenderResult.None"/>。
	/// 不明な ContentType は PNG にフォールバック。
	/// </summary>
	public static HorizontalTimetableRenderResult Build(Work? work)
	{
		if (!HasHorizontalTimetable(work))
			return HorizontalTimetableRenderResult.None;

		byte[] content = work!.ETrainTimetableContent!;

		int contentTypeValue = work.ETrainTimetableContentType ?? (int)ContentType.PNG;
		ContentType contentType = Enum.IsDefined(typeof(ContentType), contentTypeValue)
			? (ContentType)contentTypeValue
			: ContentType.PNG;

		return contentType switch
		{
			ContentType.JPG => HorizontalTimetableRenderResult.Html(BuildImageHtml(content, "image/jpeg")),
			ContentType.PDF => HorizontalTimetableRenderResult.Html(BuildPdfHtml(content)),
			ContentType.URI => HorizontalTimetableRenderResult.Uri(System.Text.Encoding.UTF8.GetString(content)),
			// Text は仕様上想定外。安全側として PNG として扱う。
			_ => HorizontalTimetableRenderResult.Html(BuildImageHtml(content, "image/png")),
		};
	}

	internal static string BuildImageHtml(byte[] content, string mimeType)
	{
		string dataUri = "data:" + mimeType + ";base64," + Convert.ToBase64String(content);
		return
			"<!DOCTYPE html>\n" +
			"<html>\n" +
			"<head>\n" +
			"\t<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, user-scalable=yes\">\n" +
			"\t<style>\n" +
			"\t\thtml, body { margin: 0; padding: 0; height: 100%; width: 100%; background-color: transparent; }\n" +
			"\t\tbody { display: flex; justify-content: center; align-items: center; }\n" +
			"\t\timg { max-width: 100%; max-height: 100%; object-fit: contain; }\n" +
			"\t</style>\n" +
			"</head>\n" +
			"<body>\n" +
			"\t<img src=\"" + dataUri + "\" alt=\"Horizontal Timetable\" />\n" +
			"</body>\n" +
			"</html>";
	}

	internal static string BuildPdfHtml(byte[] content)
	{
		// NOTE: Android WebView は data:application/pdf URI のレンダリングを
		// サポートしないため、Android 上では空白表示になる既知の制約あり。
		// 将来的にはキャッシュディレクトリへ書き出して file:// で読み込むか、
		// プラットフォーム別ビューワに切り替える必要がある。
		string dataUri = "data:application/pdf;base64," + Convert.ToBase64String(content);
		return
			"<!DOCTYPE html>\n" +
			"<html>\n" +
			"<head>\n" +
			"\t<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n" +
			"\t<style>\n" +
			"\t\tbody { margin: 0; padding: 0; }\n" +
			"\t\tembed, iframe { width: 100%; height: 100%; border: none; }\n" +
			"\t</style>\n" +
			"</head>\n" +
			"<body>\n" +
			"\t<embed src=\"" + dataUri + "\" type=\"application/pdf\" />\n" +
			"</body>\n" +
			"</html>";
	}
}
