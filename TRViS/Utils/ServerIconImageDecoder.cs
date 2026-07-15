using System.Text;

using SkiaSharp;

using Svg.Skia;

using TRViS.Services;

namespace TRViS.Utils;

/// <summary>
/// <c>ServerInfo.IconImage</c> / <c>IconImageDark</c> (base64, data URI 可) を
/// <see cref="ImageSource"/> にデコードする。png / jpg / gif はそのままストリームとして
/// <see cref="ImageSource.FromStream(Func{Stream})"/> に渡し、svg (image/svg+xml) は
/// SkiaSharp + Svg.Skia で表示サイズにラスタライズしてから PNG として渡す。
/// 不正な値でも例外を投げず false を返し、呼び出し側は表示を諦めるだけで済む。
/// </summary>
internal static class ServerIconImageDecoder
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	/// <param name="targetPixelWidth">SVG をラスタライズする際の目標幅 (px)。表示領域より
	/// 大きい SVG はこのサイズに収まるよう縮小される。png/jpg/gif はここで縮小せず、
	/// 表示側の Aspect=AspectFit + 固定 Width/HeightRequest で縮小表示する。</param>
	public static bool TryDecode(string? base64OrDataUri, int targetPixelWidth, int targetPixelHeight, out ImageSource? source)
	{
		source = null;
		if (string.IsNullOrEmpty(base64OrDataUri))
			return false;

		try
		{
			int commaIndex = base64OrDataUri.IndexOf(',');
			bool isDataUri = base64OrDataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0;
			string mime = isDataUri ? base64OrDataUri[5..commaIndex].Split(';')[0] : string.Empty;
			string payload = isDataUri ? base64OrDataUri[(commaIndex + 1)..] : base64OrDataUri;

			byte[] bytes = Convert.FromBase64String(payload);

			bool isSvg = mime.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase)
				|| (!isDataUri && LooksLikeSvg(bytes));

			byte[] displayBytes = isSvg
				? RasterizeSvgToPng(bytes, targetPixelWidth, targetPixelHeight)
				: bytes;

			source = ImageSource.FromStream(() => new MemoryStream(displayBytes));
			return true;
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "Failed to decode server icon image");
			return false;
		}
	}

	// data URI の mime が無い (プレーン base64) 場合だけのフォールバック検出。
	private static bool LooksLikeSvg(byte[] bytes)
	{
		int len = Math.Min(bytes.Length, 1024);
		string head = Encoding.UTF8.GetString(bytes, 0, len);
		return head.Contains("<svg", StringComparison.OrdinalIgnoreCase);
	}

	private static byte[] RasterizeSvgToPng(byte[] svgBytes, int targetWidth, int targetHeight)
	{
		using var svg = new SKSvg();
		using var svgStream = new MemoryStream(svgBytes);
		var picture = svg.Load(svgStream) ?? throw new InvalidOperationException("Failed to parse SVG");

		var bounds = picture.CullRect;
		float scale = bounds.Width <= 0 || bounds.Height <= 0
			? 1f
			: Math.Min(targetWidth / bounds.Width, targetHeight / bounds.Height);

		int bitmapWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
		int bitmapHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));

		using var bitmap = new SKBitmap(bitmapWidth, bitmapHeight);
		using (var canvas = new SKCanvas(bitmap))
		{
			canvas.Clear(SKColors.Transparent);
			canvas.Scale(scale);
			canvas.DrawPicture(picture);
			canvas.Flush();
		}

		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}
}
