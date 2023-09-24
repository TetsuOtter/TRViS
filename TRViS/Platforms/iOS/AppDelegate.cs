using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace TRViS;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	//ref: https://github.com/dotnet/maui/issues/4230#issuecomment-1143228199
	public AppDelegate() : base()
	{
		LabelHandler.Mapper.AppendToMapping(nameof(Label.Text), UpdateHtmlText);
		LabelHandler.Mapper.AppendToMapping(nameof(Label.FontFamily), UpdateHtmlText);
		LabelHandler.Mapper.AppendToMapping(nameof(Label.FontSize), UpdateHtmlText);
		LabelHandler.Mapper.AppendToMapping(nameof(Label.TextType), UpdateHtmlText);
	}

	static void UpdateHtmlText(ILabelHandler handler, ILabel _label)
	{
		if (_label is not Label label || label.TextType != TextType.Html)
			return;

		Microsoft.Maui.Font font = label.ToFont();
		IFontRegistrar? registrar = handler.MauiContext!.Services.GetService<IFontRegistrar>();

		string? fontName = CleanseFontName(font.Family, registrar);
		double fontSize = label.FontSize != 0 ? label.FontSize : UIFont.SystemFontSize;

		NSError? nsError = null;
		handler.PlatformView.AttributedText = new(
			$"<span style=\"font-family: '{fontName}'; font-size: {fontSize};\">{label.Text}</span>",
			new()
			{
				DocumentType = NSDocumentType.HTML,
				StringEncoding = NSStringEncoding.UTF8,
			},
			#pragma warning disable CS8601
			ref nsError);
			#pragma warning restore CS8601
	}

	static string? CleanseFontName(string? fontName, IFontRegistrar? fontRegistrar)
	{
		if (fontName is null || fontRegistrar is null)
			return null;

		if (fontRegistrar.GetFont(fontName) is string fontPostScriptName)
			return fontPostScriptName;

		var fontFile = FontFile.FromString(fontName);

		if (!string.IsNullOrWhiteSpace(fontFile.Extension))
		{
			if (fontRegistrar.GetFont(fontFile.FileNameWithExtension()) is string filePath)
				return filePath ?? fontFile.PostScriptName;
		}
		else
		{
			foreach (var ext in FontFile.Extensions)
			{
				var formatted = fontFile.FileNameWithExtension(ext);
				if (fontRegistrar.GetFont(formatted) is string filePath)
					return filePath;
			}
		}

		return fontFile.PostScriptName;
	}
}
