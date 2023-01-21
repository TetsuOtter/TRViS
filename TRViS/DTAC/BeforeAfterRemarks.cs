using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;

namespace TRViS.DTAC;

public class BeforeAfterRemarks
{
	Grid Parent;

	public BeforeAfterRemarks(Grid Parent)
	{
		this.Parent = Parent;

		IsVisible = false;

		Grid.SetColumn(Label, 2);
		Grid.SetColumnSpan(Label, 6);

		Label.Margin = new(32, 0);
		Label.HorizontalOptions = LayoutOptions.Start;
		Label.LineHeight = 1.4;
	}

	readonly HtmlAutoDetectLabel Label = DTACElementStyles.LabelStyle<HtmlAutoDetectLabel>();
	readonly Line Separator = DTACElementStyles.HorizontalSeparatorLineStyle();

	public string Text
	{
		get => Label.Text;
		set
		{
			if (Label.Text == value)
				return;

			IsVisible = !string.IsNullOrWhiteSpace(value);

			Label.Text = value;
		}
	}

	public void AddToParent()
	{
		Parent.Add(Label);
		Parent.Add(Separator);
	}

	bool _IsVisible = false;
	public bool IsVisible
	{
		get => _IsVisible;
		set
		{
			_IsVisible = value;
			Label.IsVisible = value;
			Separator.IsVisible = value;
		}
	}

	public void IsElementUnderThisVisible(in bool isVisible)
	{
		Separator.IsVisible = _IsVisible || isVisible;
	}

	public void SetRow(in int row)
	{
		Grid.SetRow(Label, row);
		Grid.SetRow(Separator, row);
	}
}

