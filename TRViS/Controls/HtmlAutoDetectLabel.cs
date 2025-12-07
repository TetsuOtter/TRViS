using System.Runtime.CompilerServices;

using TR.BBCodeLabel.Maui;

using TRViS.Services;
using TRViS.Utils;

namespace TRViS.Controls;

public class HtmlAutoDetectLabel : ContentView
{
	private readonly HtmlAutoDetectLabelImpl htmlAutoDetectLabelImpl = new();
	private readonly BBCodeLabel bbCodeLabel = new();

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
			Content = htmlAutoDetectLabelImpl;
			htmlAutoDetectLabelImpl.Text = Text;
		}
		else
		{
			Content = bbCodeLabel;
			bbCodeLabel.BBCodeText = Text;
			// FIXME: 本来はBBCodeLabel側でやるべきだが、一旦ここで対応する
			foreach (var v in bbCodeLabel.FormattedText.Spans)
			{
				v.FontAutoScalingEnabled = FontAutoScalingEnabled;
			}
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
				Util.ExitWithAlert(ex);
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
					Util.ExitWithAlert(ex);
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
