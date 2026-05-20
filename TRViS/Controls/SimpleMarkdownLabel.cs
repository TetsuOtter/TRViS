using System.Text;

using DependencyPropertyGenerator;

using TRViS.Services;
using TRViS.Utils;

namespace TRViS.Controls;

[DependencyProperty<string>("MarkdownFileContent")]
public partial class SimpleMarkdownLabel : Label
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	const int H0_FONT_SIZE = 28;
	const int HEADER_FONT_SIZE_STEP = 3;
	const double HEADER_LINE_HEIGHT = 1.25;
	const int MAX_HEADER_LEVEL = 3;

	// Resource key whose value ("NotoSansJPRegular") is the bundled Noto
	// Sans JP alias registered in MauiProgram.ConfigureFonts. The resource
	// itself is injected by App.xaml.cs (XamlC can't materialize
	// <sys:String> in App.xaml — XC0004 on System.String).
	// The implicit <Style TargetType="Label"> in Styles.xaml has no
	// ApplyToDerivedTypes, so this Label subclass — and the Spans it builds
	// — never inherit {DynamicResource DefaultFontFamily}. Without an
	// explicit FontFamily the privacy-policy Markdown falls back to the iOS
	// system CJK font (PingFang SC — Simplified-Chinese glyph shapes) for
	// BOTH Japanese and English captures, which is the screenshot diff.
	const string DefaultFontFamilyResourceKey = "DefaultFontFamily";

	public SimpleMarkdownLabel()
	{
		logger.Trace("Creating...");
		LineHeight = 1.1;
		LineBreakMode = LineBreakMode.WordWrap;
		this.SetDynamicResource(Label.FontFamilyProperty, DefaultFontFamilyResourceKey);
		logger.Trace("Created.");
	}

	// Belt-and-suspenders: a Span does not reliably inherit FontFamily from
	// its parent Label across MAUI versions, so pin it on every generated
	// Span too (see DefaultFontFamilyResourceKey rationale above).
	static Span WithDefaultFont(Span span)
	{
		span.SetDynamicResource(Span.FontFamilyProperty, DefaultFontFamilyResourceKey);
		return span;
	}

	partial void OnMarkdownFileContentChanged(string? newValue)
	{
		try
		{
			if (string.IsNullOrEmpty(newValue))
			{
				logger.Trace("Text is null or empty.");
				if (MainThread.IsMainThread)
					FormattedText = null;
				else
					MainThread.BeginInvokeOnMainThread(() => FormattedText = null);
				return;
			}

			logger.Debug("Text: {0}", newValue);
			FormattedString formattedString = new();
			SetMarkdownSpanList(newValue, formattedString.Spans);
			if (MainThread.IsMainThread)
			{
				FormattedText = formattedString;
				logger.Trace("FormattedText Updated: Count={0}", FormattedText.Spans.Count);
			}
			else
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					try
					{
						FormattedText = formattedString;
						logger.Trace("FormattedText Updated: Count={0}", FormattedText.Spans.Count);
					}
					catch (Exception ex)
					{
						logger.Warn(ex, "Unknown Exception");
						InstanceManager.CrashlyticsWrapper.Log(ex, "SimpleMarkdownLabel.OnMarkdownFileContentChanged (set FormattedText)");
					}
				});
			}
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "HtmlAutoDetectLabel.OnPropertyChanged");
			Util.ExitWithAlertAsync(ex);
		}
	}

	static void SetMarkdownSpanList(in string inputText, IList<Span> spanList)
	{
		try
		{
			string[] inputTextArr = inputText.Split('\n');

			static Span FlushToSpan(StringBuilder sb)
			{
				if (sb.Length == 0)
					return WithDefaultFont(new Span());

				logger.Trace(">> 0 < sb.Length");
				Span span = new()
				{
					Text = sb.ToString(),
				};
				sb.Clear();
				return WithDefaultFont(span);
			}

			StringBuilder sb = new();
			foreach (string inputTextLine in inputTextArr)
			{
				bool isHeaderLine = inputTextLine.StartsWith('#');
				bool isUrlLine = inputTextLine.StartsWith("https://");

				logger.Trace(
					"inputTextLine(header:{1}, url:{2}): '{0}'",
					inputTextLine,
					isHeaderLine,
					isUrlLine
				);
				if (!isHeaderLine && !isUrlLine)
				{
					logger.Trace("> normal text");
					sb.AppendLine(inputTextLine);
					continue;
				}
				if (0 < sb.Length)
					spanList.Add(FlushToSpan(sb));
				if (isHeaderLine)
				{
					int headerLevel = 0;
					for (int i = 0; i < MAX_HEADER_LEVEL; i++)
					{
						if (inputTextLine[i] != '#')
							break;
						headerLevel++;
					}
					logger.Trace("> headerLevel: '{0}'", headerLevel);

					spanList.Add(WithDefaultFont(new Span
					{
						Text = inputTextLine[headerLevel..].Trim(),
						FontSize = H0_FONT_SIZE - headerLevel * HEADER_FONT_SIZE_STEP,
						LineHeight = HEADER_LINE_HEIGHT,
					}));
				}
				else if (isUrlLine)
				{
					Span linkSpan = new()
					{
						Text = inputTextLine + '\n',
						FontAttributes = FontAttributes.Italic,
						TextDecorations = TextDecorations.Underline,
						TextColor = Colors.Aqua,
						GestureRecognizers =
						{
							new TapGestureRecognizer
							{
								Command = Util.OpenUrlCommand,
								CommandParameter = inputTextLine,
							},
						},
					};
					linkSpan.SetAppThemeColor(Span.TextColorProperty, Colors.Blue, Colors.Aqua);
					spanList.Add(WithDefaultFont(linkSpan));
				}
				else
				{
					logger.Trace("> normal text");
					sb.AppendLine(inputTextLine);
				}
			}

			if (0 < sb.Length)
				spanList.Add(FlushToSpan(sb));

			logger.Debug("spanList.Count: {0}", spanList.Count);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SimpleMarkdownLabel.SetMarkdownSpanList");
			Util.ExitWithAlertAsync(ex);
		}
	}
}
