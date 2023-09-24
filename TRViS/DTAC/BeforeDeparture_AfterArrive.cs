using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;

namespace TRViS.DTAC;

public class BeforeDeparture_AfterArrive
{
	public readonly RowDefinition RowDefinition = new(DTACElementStyles.BeforeDeparture_AfterArrive_Height);

	readonly BoxView HeaderBoxView = new()
	{
		IsVisible = false,
		Margin = new(0),
	};
	readonly Label HeaderLabel = DTACElementStyles.HeaderLabelStyle<Label>();

	readonly Line UpperSeparator = DTACElementStyles.HorizontalSeparatorLineStyle();
	readonly Line LowerSeparator = DTACElementStyles.HorizontalSeparatorLineStyle();

	readonly HtmlAutoDetectLabel Label = DTACElementStyles.LabelStyle<HtmlAutoDetectLabel>();
	readonly HtmlAutoDetectLabel Label_OnStationTrackColumn = DTACElementStyles.LabelStyle<HtmlAutoDetectLabel>();

	Grid Parent { get; }
	bool AlwaysShow { get; }

	public BeforeDeparture_AfterArrive(Grid Parent, string HeaderLabelText)
		: this(Parent, HeaderLabelText, false) { }

	public BeforeDeparture_AfterArrive(Grid Parent, string HeaderLabelText, bool AlwaysShow)
	{
		this.Parent = Parent;
		this.AlwaysShow = AlwaysShow;

		UpperSeparator.VerticalOptions = LayoutOptions.Start;

		Label.HorizontalOptions = LayoutOptions.Start;
		Label_OnStationTrackColumn.HorizontalOptions = LayoutOptions.Start;

		DTACElementStyles.HeaderTextColor.Apply(HeaderLabel, Microsoft.Maui.Controls.Label.TextColorProperty);
		DTACElementStyles.HeaderBackgroundColor.Apply(HeaderBoxView, BoxView.ColorProperty);

		HeaderLabel.Text = HeaderLabelText;

		IsVisible = AlwaysShow;
	}

	public string Text
	{
		get => Label.Text;
		set
		{
			if (Label.Text == value)
				return;

			if (!AlwaysShow)
				IsVisible = !string.IsNullOrWhiteSpace(value);

			Label.Text = value;
		}
	}

	public string Text_OnStationTrackColumn
	{
		get => Label_OnStationTrackColumn.Text;
		set => Label_OnStationTrackColumn.Text = value;
	}

	public void AddToParent()
	{
		Parent.Add(HeaderBoxView);
		Parent.Add(HeaderLabel);
		Parent.AddWithSpan(
			Label,
			column: 1,
			columnSpan: 7
		);
		Parent.AddWithSpan(
			Label_OnStationTrackColumn,
			column: 4,
			columnSpan: 4
		);
		DTACElementStyles.AddHorizontalSeparatorLineStyle(Parent, LowerSeparator, 0);
		DTACElementStyles.AddHorizontalSeparatorLineStyle(Parent, UpperSeparator, 0);
	}

	bool _IsVisible = false;
	public bool IsVisible
	{
		get => _IsVisible;
		set
		{
			if (_IsVisible == value)
				return;

			_IsVisible = value;

			HeaderBoxView.IsVisible
				= HeaderLabel.IsVisible
				= LowerSeparator.IsVisible
				= UpperSeparator.IsVisible
				= Label.IsVisible
				= Label_OnStationTrackColumn.IsVisible
				= value;
		}
	}

	public void SetRow(in int row)
	{
		Grid.SetRow(HeaderBoxView, row);
		Grid.SetRow(HeaderLabel, row);
		Grid.SetRow(LowerSeparator, row);
		Grid.SetRow(UpperSeparator, row);
		// Parent.SetRow(Label, row);
		// Parent.SetRow(Label_OnStationTrackColumn, row);
		Console.WriteLine($"SetRow: {row} (Label: {Label.Text})");
		Parent.Remove(Label);
		Parent.Remove(Label_OnStationTrackColumn);
		Parent.AddWithSpan(
			Label,
			row: row,
			column: 1,
			columnSpan: 7
		);
		Parent.AddWithSpan(
			Label_OnStationTrackColumn,
			row: row,
			column: 4,
			columnSpan: 4
		);
	}
}

