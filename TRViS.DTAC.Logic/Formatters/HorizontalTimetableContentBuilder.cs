using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Formatters;

/// <summary>
/// Discriminated result describing how the horizontal timetable should be rendered.
/// View 層がそれぞれの種別に応じて HTML / URL を組み立てて WebView に流す。
/// </summary>
public enum HorizontalTimetableRenderKind
{
	None,
	/// <summary>外部 URL。Payload はそのまま WebView に渡す URL 文字列。</summary>
	Uri,
	/// <summary>PDF バイナリ。Payload は base64 文字列。View 層で PDF.js Viewer HTML を組み立てる。</summary>
	Pdf,
	/// <summary>PNG バイナリ。Payload は base64 文字列。</summary>
	Png,
	/// <summary>JPEG バイナリ。Payload は base64 文字列。</summary>
	Jpg,
}

/// <summary>
/// 横型時刻表 (PNG/JPG/PDF/URI) を表示用の生データに変換した結果。
/// HTML の組み立ては View 層の責務とし、ここでは「何を表示すべきか」だけを返す。
/// </summary>
public readonly record struct HorizontalTimetableRenderResult(
	HorizontalTimetableRenderKind Kind,
	string Payload)
{
	public static HorizontalTimetableRenderResult None { get; } = new(HorizontalTimetableRenderKind.None, string.Empty);
	public static HorizontalTimetableRenderResult Uri(string uri) => new(HorizontalTimetableRenderKind.Uri, uri);
	public static HorizontalTimetableRenderResult Pdf(byte[] bytes) => new(HorizontalTimetableRenderKind.Pdf, Convert.ToBase64String(bytes));
	public static HorizontalTimetableRenderResult Png(byte[] bytes) => new(HorizontalTimetableRenderKind.Png, Convert.ToBase64String(bytes));
	public static HorizontalTimetableRenderResult Jpg(byte[] bytes) => new(HorizontalTimetableRenderKind.Jpg, Convert.ToBase64String(bytes));
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
			ContentType.JPG => HorizontalTimetableRenderResult.Jpg(content),
			ContentType.PDF => HorizontalTimetableRenderResult.Pdf(content),
			ContentType.URI => HorizontalTimetableRenderResult.Uri(System.Text.Encoding.UTF8.GetString(content)),
			// Text は仕様上想定外。安全側として PNG として扱う。
			_ => HorizontalTimetableRenderResult.Png(content),
		};
	}
}
