using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;

namespace TRViS.DTAC;

public class BeforeAfterRemarks
{
	readonly Grid Parent;

	public BeforeAfterRemarks(Grid Parent)
	{
		this.Parent = Parent;

		IsVisible = false;

		Label.Margin = new(32, 0);
		Label.HorizontalOptions = LayoutOptions.Start;
		Label.LineHeight = 1.4;
	}

	readonly HtmlAutoDetectLabel Label = DTACElementStyles.LabelStyle<HtmlAutoDetectLabel>();

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
		Parent.AddWithSpan(
			Label,
			column: 2,
			columnSpan: 6
		);
	}

	bool _IsVisible = false;
	public bool IsVisible
	{
		get => _IsVisible;
		set
		{
			_IsVisible = value;
			Label.IsVisible = value;
		}
	}

	public void SetRow(in int row)
	{
		// Grid.SetRow(Label, row);
		Parent.SetRow(Label, row);
	}
}

