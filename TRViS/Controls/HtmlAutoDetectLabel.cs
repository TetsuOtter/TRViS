using System.Runtime.CompilerServices;

namespace TRViS.Controls;

public class HtmlAutoDetectLabel : Label
{
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

				try
				{
					TextType = (text.StartsWith('<') && text.EndsWith('>')) ? TextType.Html : TextType.Text;
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
