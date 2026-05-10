namespace TRViS.Services;

/// <summary>
/// PNG / JPEG (base64) を WebView 表示用にラップする HTML を組み立てる。
/// </summary>
internal static class HorizontalTimetableImageHtmlBuilder
{
	public static string BuildPng(string base64) => Build(base64, "image/png");
	public static string BuildJpg(string base64) => Build(base64, "image/jpeg");

	private static string Build(string base64, string mimeType)
	{
		string dataUri = "data:" + mimeType + ";base64," + base64;
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
}
