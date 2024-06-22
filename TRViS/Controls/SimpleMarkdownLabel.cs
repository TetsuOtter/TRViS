using System.Runtime.CompilerServices;
using System.Text;
using DependencyPropertyGenerator;
using Microsoft.AppCenter.Crashes;

namespace TRViS.Controls;

[DependencyProperty<string>("MarkdownFileContent")]
public partial class SimpleMarkdownLabel : Label
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	const int H0_FONT_SIZE = 28;
	const int HEADER_FONT_SIZE_STEP = 3;
	const double HEADER_LINE_HEIGHT = 1.25;
	const int MAX_HEADER_LEVEL = 3;

	public SimpleMarkdownLabel()
	{
		logger.Trace("Creating...");
		LineHeight = 1.1;
		LineBreakMode = LineBreakMode.WordWrap;
		logger.Trace("Created.");
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
				MainThread.BeginInvokeOnMainThread(() => {
					try
					{
						FormattedText = formattedString;
						logger.Trace("FormattedText Updated: Count={0}", FormattedText.Spans.Count);
					}
					catch (Exception ex)
					{
						logger.Warn(ex, "Unknown Exception");
						Crashes.TrackError(ex);
					}
				});
			}
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			Crashes.TrackError(ex);
			Utils.ExitWithAlert(ex);
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
					return new Span();
				
				logger.Trace(">> 0 < sb.Length");
				Span span = new()
				{
					Text = sb.ToString(),
				};
				sb.Clear();
				return span;
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

					spanList.Add(new Span
					{
						Text = inputTextLine[headerLevel..].Trim(),
						FontSize = H0_FONT_SIZE - headerLevel * HEADER_FONT_SIZE_STEP,
						LineHeight = HEADER_LINE_HEIGHT,
					});
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
								Command = Utils.OpenUrlCommand,
								CommandParameter = inputTextLine,
							},
						},
					};
					linkSpan.SetAppThemeColor(Span.TextColorProperty, Colors.Blue, Colors.Aqua);
					spanList.Add(linkSpan);
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
			Crashes.TrackError(ex);
			Utils.ExitWithAlert(ex);
		}
	}
}
