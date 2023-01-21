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
		Color = DTACElementStyles.HeaderBackgroundColor,
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

		Grid.SetColumn(Label, 1);
		Grid.SetColumnSpan(Label, 7);

		Grid.SetColumn(Label_OnStationTrackColumn, 4);
		Grid.SetColumnSpan(Label_OnStationTrackColumn, 4);

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
		Parent.Add(Label);
		Parent.Add(Label_OnStationTrackColumn);
		Parent.Add(LowerSeparator);
		Parent.Add(UpperSeparator);
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
		Grid.SetRow(Label, row);
		Grid.SetRow(Label_OnStationTrackColumn, row);
	}
}

