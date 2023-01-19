using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;

namespace TRViS.DTAC;

public partial class TrainInfo_BeforeDeparture : Grid
{
	public const int DEFAULT_ROW_HEIGHT = 48;
	static readonly RowDefinitionCollection DefaultRowDefinitions = new()
	{
		new RowDefinition(DEFAULT_ROW_HEIGHT),
		new RowDefinition(DEFAULT_ROW_HEIGHT)
	};

	static readonly ColumnDefinitionCollection DefaultColumnDefinitions = new()
	{
		new ColumnDefinition(VerticalStylePage.RUN_TIME_COLUMN_WIDTH),
		new ColumnDefinition(140 * 3),
		new ColumnDefinition(new(1, GridUnitType.Star)),
	};

	#region TrainInfo Area
	readonly HtmlAutoDetectLabel TrainInfoArea = DTACElementStyles.LargeLabelStyle<HtmlAutoDetectLabel>();

	public string TrainInfoText
	{
		get => TrainInfoArea.Text;
		set => TrainInfoArea.Text = value;
	}
	#endregion

	#region TrainInfo Area
	readonly HtmlAutoDetectLabel BeforeDepartureArea = DTACElementStyles.LabelStyle<HtmlAutoDetectLabel>();
	readonly HtmlAutoDetectLabel BeforeDepartureArea_OnStationTrackColumn = DTACElementStyles.LabelStyle<HtmlAutoDetectLabel>();

	public string BeforeDepartureText
	{
		get => BeforeDepartureArea.Text;
		set => BeforeDepartureArea.Text = value;
	}

	public string BeforeDepartureText_OnStationTrackColumn
	{
		get => BeforeDepartureArea_OnStationTrackColumn.Text;
		set => BeforeDepartureArea_OnStationTrackColumn.Text = value;
	}
	#endregion

	public TrainInfo_BeforeDeparture()
	{
		RowDefinitions = DefaultRowDefinitions;
		ColumnDefinitions = DefaultColumnDefinitions;

		Line trainInfoAreaSeparator = DTACElementStyles.HorizontalSeparatorLineStyle();
		TrainInfoArea.HorizontalOptions = LayoutOptions.Start;

		// BeforeDepartureArea
		Line beforeDepartureAreaSeparator = DTACElementStyles.HorizontalSeparatorLineStyle();
		BoxView beforeDepartureHeaderBoxView = new()
		{
			BackgroundColor = DTACElementStyles.HeaderBackgroundColor,
			Color = DTACElementStyles.HeaderBackgroundColor,
			Margin = new(0),
		};
		Label beforeDepartureHeaderLabel = DTACElementStyles.HeaderLabelStyle<Label>();
		beforeDepartureHeaderLabel.HorizontalOptions = LayoutOptions.Center;
		beforeDepartureHeaderLabel.Text = "発前";

		BeforeDepartureArea.HorizontalOptions = LayoutOptions.Start;
		BeforeDepartureArea_OnStationTrackColumn.HorizontalOptions = LayoutOptions.Start;

		Grid.SetColumnSpan(trainInfoAreaSeparator, 3);
		Grid.SetColumnSpan(TrainInfoArea, 3);
		this.Add(
			trainInfoAreaSeparator,
			row: 0
		);
		this.Add(
			TrainInfoArea,
			row: 0
		);

		Grid.SetColumnSpan(beforeDepartureAreaSeparator, 3);
		Grid.SetColumnSpan(BeforeDepartureArea, 2);
		this.Add(
			beforeDepartureAreaSeparator,
			row: 1
		);
		this.Add(
			beforeDepartureHeaderBoxView,
			row: 1
		);
		this.Add(
			beforeDepartureHeaderLabel,
			row: 1
		);
		this.Add(
			BeforeDepartureArea,
			column: 1,
			row: 1
		);
		this.Add(
			BeforeDepartureArea_OnStationTrackColumn,
			column: 2,
			row: 1
		);
	}
}
