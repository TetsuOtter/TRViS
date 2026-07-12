using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using TR.BBCodeLabel.Maui;

using TRViS.Services;
using TRViS.Utils;

namespace TRViS.Controls;

public partial class HtmlAutoDetectLabel : ContentView
{
	private readonly HtmlAutoDetectLabelImpl htmlAutoDetectLabelImpl = new();
	private readonly BBCodeLabel bbCodeLabel = new();

	// .NET MAUI's native Label TextType=Html rendering has a duplicate-render bug
	// on iOS: some HTML input renders as an overlapping double-exposure of the
	// same text instead of once (observed with <span style="color:...">, e.g.
	// timetable info rows like "交直切換"). Since BBCodeLabel renders the
	// equivalent styling correctly, simple/known HTML constructs are converted
	// to BBCode and routed there instead; only markup we don't recognize still
	// falls back to the native HTML label.
	[GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
	private static partial Regex BrTagRegex();
	[GeneratedRegex("""<span\s+style\s*=\s*"color:\s*([^"]+?)\s*"\s*>(.*?)</span>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex ColoredSpanRegex();
	[GeneratedRegex(@"<span\s*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex PlainSpanRegex();
	[GeneratedRegex(@"<b\s*>(.*?)</b\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex BoldTagRegex();
	[GeneratedRegex(@"<[^>]+>")]
	private static partial Regex AnyTagRegex();

	/// <summary>
	/// Converts known-simple HTML (color spans, plain spans, &lt;b&gt;, &lt;br/&gt;)
	/// to the equivalent BBCode. Returns null if unrecognized markup remains, so
	/// the caller can fall back to the native HTML label for content this doesn't
	/// understand.
	/// </summary>
	private static string? TryConvertSimpleHtmlToBBCode(string html)
	{
		string s = BrTagRegex().Replace(html, "\n");
		s = ColoredSpanRegex().Replace(s, "[color=$1]$2[/color]");
		s = PlainSpanRegex().Replace(s, "$1");
		s = BoldTagRegex().Replace(s, "[b]$1[/b]");
		return AnyTagRegex().IsMatch(s) ? null : s;
	}

	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(HtmlAutoDetectLabel), default(string),
			propertyChanged: OnTextPropertyChanged);

	private static void OnTextPropertyChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is HtmlAutoDetectLabel label)
		{
			label.OnChangeText();
		}
	}

	string? _text = string.Empty;
	public string? Text
	{
		get => _text;
		set
		{
			if (_text == value)
				return;
			_text = value;
			OnChangeText();
		}
	}

	[Obsolete("Use LabelStyle property instead.", true)]
	public new Style? Style => base.Style;

	public Style? LabelStyle
	{
		get => htmlAutoDetectLabelImpl.Style;
		set
		{
			htmlAutoDetectLabelImpl.Style = value;
			bbCodeLabel.Style = value;
		}
	}

	public AppThemeColorBindingExtension? CurrentAppThemeColorBindingExtension
	{
		get => htmlAutoDetectLabelImpl.CurrentAppThemeColorBindingExtension;
		set
		{
			htmlAutoDetectLabelImpl.CurrentAppThemeColorBindingExtension = value;
			bbCodeLabel.DefaultLightThemeTextColor = value?.Light;
			bbCodeLabel.DefaultDarkThemeTextColor = value?.Dark;
		}
	}

	public Color TextColor
	{
		get => htmlAutoDetectLabelImpl.TextColor;
		set
		{
			htmlAutoDetectLabelImpl.TextColor = value;
			bbCodeLabel.TextColor = value;
		}
	}
	public double FontSize
	{
		get => htmlAutoDetectLabelImpl.FontSize;
		set
		{
			htmlAutoDetectLabelImpl.FontSize = value;
			bbCodeLabel.FontSize = value;
		}
	}
	public string FontFamily
	{
		get => htmlAutoDetectLabelImpl.FontFamily;
		set
		{
			htmlAutoDetectLabelImpl.FontFamily = value;
			bbCodeLabel.FontFamily = value;
		}
	}
	public LineBreakMode LineBreakMode
	{
		get => htmlAutoDetectLabelImpl.LineBreakMode;
		set
		{
			htmlAutoDetectLabelImpl.LineBreakMode = value;
			bbCodeLabel.LineBreakMode = value;
		}
	}
	public double LineHeight
	{
		get => htmlAutoDetectLabelImpl.LineHeight;
		set
		{
			htmlAutoDetectLabelImpl.LineHeight = value;
			bbCodeLabel.LineHeight = value;
		}
	}
	public bool FontAutoScalingEnabled
	{
		get => htmlAutoDetectLabelImpl.FontAutoScalingEnabled;
		set
		{
			htmlAutoDetectLabelImpl.FontAutoScalingEnabled = value;
			bbCodeLabel.FontAutoScalingEnabled = value;
		}
	}
	public FontAttributes FontAttributes
	{
		get => htmlAutoDetectLabelImpl.FontAttributes;
		set
		{
			htmlAutoDetectLabelImpl.FontAttributes = value;
			bbCodeLabel.FontAttributes = value;
		}
	}
	public TextAlignment HorizontalTextAlignment
	{
		get => htmlAutoDetectLabelImpl.HorizontalTextAlignment;
		set
		{
			htmlAutoDetectLabelImpl.HorizontalTextAlignment = value;
			bbCodeLabel.HorizontalTextAlignment = value;
		}
	}

	private void OnChangeText()
	{
		if (string.IsNullOrEmpty(Text))
		{
			Content = null;
			return;
		}
		string trimmedText = Text.Trim();
		if (trimmedText.StartsWith('<') && trimmedText.EndsWith('>'))
		{
			string? bbcode = TryConvertSimpleHtmlToBBCode(trimmedText);
			if (bbcode is not null)
			{
				ShowAsBBCode(bbcode);
				return;
			}
			Content = htmlAutoDetectLabelImpl;
			htmlAutoDetectLabelImpl.Text = Text;
		}
		else
		{
			ShowAsBBCode(Text);
		}
	}

	private void ShowAsBBCode(string bbcode)
	{
		Content = bbCodeLabel;
		bbCodeLabel.BBCodeText = bbcode;
		// FIXME: 本来はBBCodeLabel側でやるべきだが、一旦ここで対応する
		foreach (var v in bbCodeLabel.FormattedText.Spans)
		{
			v.FontAutoScalingEnabled = FontAutoScalingEnabled;
		}
	}

	private class HtmlAutoDetectLabelImpl : Label
	{
		private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
		public AppThemeColorBindingExtension? CurrentAppThemeColorBindingExtension { get; set; }
		public Color? LastTextColor { get; private set; }

		protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			try
			{
				base.OnPropertyChanged(propertyName);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "HtmlAutoDetectLabel.OnPropertyChanged (base)");
				Util.ExitWithAlertAsync(ex);
			}

			if (propertyName == nameof(Text))
			{
				try
				{
					OnTextPropertyChanged();
				}
				catch (Exception ex)
				{
					logger.Fatal(ex, "Unknown Exception");
					InstanceManager.CrashlyticsWrapper.Log(ex, "HtmlAutoDetectLabel.OnPropertyChanged (Text)");
					Util.ExitWithAlertAsync(ex);
				}
			}
		}

		void OnTextPropertyChanged()
		{
			if (string.IsNullOrEmpty(Text))
			{
				logger.Debug("Text Changed -> (NullOrEmpty)");
				TextType = TextType.Text;
			}
			else
			{
				string text = Text.Trim();
				bool isColoredString = text.Contains("color:");
				logger.Trace("Text Changed -> {0} (isColoredString: {1})", text, isColoredString);

				try
				{
					TextType _textType = (text.StartsWith('<') && text.EndsWith('>')) ? TextType.Html : TextType.Text;
					if (CurrentAppThemeColorBindingExtension is not null)
					{
						if (_textType == TextType.Html && isColoredString)
						{
							logger.Trace("CurrentAppThemeColorBindingExtension is not null && TextType: Html && isColoredString: true -> AppThemeColor set to null");
							this.SetAppThemeColor(TextColorProperty, null, null);
						}
						else
						{
							logger.Trace("CurrentAppThemeColorBindingExtension is not null"
								+ " && (TextType:{0} (not Html) || isColoredString: {1} (not true))"
								+ " -> Restore AppThemeColor(Light:{2}, Dark:{3})",
								_textType,
								isColoredString,
								CurrentAppThemeColorBindingExtension.Light,
								CurrentAppThemeColorBindingExtension.Dark
							);
							CurrentAppThemeColorBindingExtension.Apply(this, TextColorProperty);
						}
					}
					else
					{
						if (_textType == TextType.Html && isColoredString)
						{
							logger.Trace("CurrentAppThemeColorBindingExtension is null && TextType: Html && isColoredString: true -> TextColor set to null");
							LastTextColor = TextColor;
							TextColor = null;
						}
						else if (TextColor is null && LastTextColor is not null)
						{
							logger.Trace("CurrentAppThemeColorBindingExtension is null"
								+ " && (TextType:{0} (not Html) || isColoredString: {1} (not true))"
								+ " && TextColor is null && LastTextColor is not null"
								+ " -> Restore TextColor({2})",
								_textType,
								isColoredString,
								LastTextColor
							);
							TextColor = LastTextColor;
						}
						else
						{
							logger.Trace("CurrentAppThemeColorBindingExtension is null"
								+ " && (TextType:{0} (not Html) || isColoredString: {1} (not true))"
								+ " && TextColor is not null"
								+ " -> Do Nothing",
								_textType,
								isColoredString
							);
						}
					}
					TextType = _textType;

					logger.Trace("Processing Complete -> TextType: {0}", TextType);
				}
				catch (Exception ex)
				{
					logger.Warn(ex, "Exception Occurred -> TextType set to Text");
					Console.WriteLine(ex);
					TextType = TextType.Text;
				}
			}
		}
	}
}
