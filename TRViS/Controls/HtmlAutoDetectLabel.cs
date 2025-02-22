using System.Runtime.CompilerServices;

namespace TRViS.Controls;

public class HtmlAutoDetectLabel : Label
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
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
			Utils.ExitWithAlert(ex);
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
				Utils.ExitWithAlert(ex);
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
