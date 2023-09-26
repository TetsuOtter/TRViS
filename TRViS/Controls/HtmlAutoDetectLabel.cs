using System.Runtime.CompilerServices;

namespace TRViS.Controls;

public class HtmlAutoDetectLabel : Label
{
	public AppThemeColorBindingExtension? CurrentAppThemeColorBindingExtension { get; set; }
	public Color? LastTextColor {get; private set; }

	protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);

		if (propertyName == nameof(Text))
		{
			if (string.IsNullOrEmpty(Text))
				TextType = TextType.Text;
			else
			{
				string text = Text.Trim();
				bool isColoredString = text.Contains("color:");

				try
				{
					TextType _textType = (text.StartsWith('<') && text.EndsWith('>')) ? TextType.Html : TextType.Text;
					if (CurrentAppThemeColorBindingExtension is not null)
					{
						if (_textType == TextType.Html && isColoredString)
						{
							this.SetAppThemeColor(TextColorProperty, null, null);
						}
						else
						{
							CurrentAppThemeColorBindingExtension.Apply(this, TextColorProperty);
						}
					}
					else
					{
						if (_textType == TextType.Html && isColoredString)
						{
							LastTextColor = TextColor;
							TextColor = null;
						}
						else if (TextColor is null && LastTextColor is not null)
						{
							TextColor = LastTextColor;
						}
					}
					TextType = _textType;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
					TextType = TextType.Text;
				}
			}
		}
	}
}
