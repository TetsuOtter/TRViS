using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;

namespace TRViS.DTAC;

public class BeforeDeparture_AfterArrive
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
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
		logger.Trace("Creating... (Parent: {0}, HeaderLabelText: {1}, AlwaysShow: {2})",
			Parent.GetType().Name,
			HeaderLabelText,
			AlwaysShow
		);

		this.Parent = Parent;
		this.AlwaysShow = AlwaysShow;

		UpperSeparator.VerticalOptions = LayoutOptions.Start;

		Label.HorizontalOptions = LayoutOptions.Start;
		Label_OnStationTrackColumn.HorizontalOptions = LayoutOptions.Start;

		DTACElementStyles.HeaderTextColor.Apply(HeaderLabel, Microsoft.Maui.Controls.Label.TextColorProperty);
		DTACElementStyles.HeaderBackgroundColor.Apply(HeaderBoxView, BoxView.ColorProperty);

		HeaderLabel.Text = HeaderLabelText;

		IsVisible = AlwaysShow;

		logger.Trace("Created");
	}

	public string Text
	{
		get => Label.Text;
		set
		{
			if (Label.Text == value)
			{
				logger.Trace("Text is the same, skipping... ({0})", value);
				return;
			}

			if (!AlwaysShow)
				IsVisible = !string.IsNullOrWhiteSpace(value);

			logger.Debug("Setting Text to {0}, AlwaysShow: {1}, IsVisible: {2}", value, AlwaysShow, IsVisible);
			Label.Text = value;
		}
	}

	public string Text_OnStationTrackColumn
	{
		get => Label_OnStationTrackColumn.Text;
		set
		{
			if (Label_OnStationTrackColumn.Text == value)
			{
				logger.Trace("Text is the same, skipping... ({0})", value);
				return;
			}

			logger.Debug("Setting Text_OnStationTrackColumn to {0}", value);
			Label_OnStationTrackColumn.Text = value;
		} 
	}

	public void AddToParent()
	{
		Parent.Add(HeaderBoxView);
		Parent.Add(HeaderLabel);
		Grid.SetColumn(Label, 1);
		Grid.SetColumn(Label_OnStationTrackColumn, 4);
		Grid.SetColumnSpan(Label, 7);
		Grid.SetColumnSpan(Label_OnStationTrackColumn, 4);
		Parent.Add(Label);
		Parent.Add(Label_OnStationTrackColumn);
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
			{
				logger.Trace("IsVisible is the same({0}), skipping...", value);
				return;
			}

			logger.Debug("Setting IsVisible to {0}", value);
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

