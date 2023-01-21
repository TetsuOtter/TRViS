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

	#region TrainInfo Area
	readonly HtmlAutoDetectLabel TrainInfoArea = DTACElementStyles.LargeLabelStyle<HtmlAutoDetectLabel>();

	public string TrainInfoText
	{
		get => TrainInfoArea.Text;
		set => TrainInfoArea.Text = value;
	}
	#endregion

	#region Before Departure Area
	readonly BeforeDeparture_AfterArrive BeforeDeparture;

	public string BeforeDepartureText
	{
		get => BeforeDeparture.Text;
		set => BeforeDeparture.Text = value;
	}

	public string BeforeDepartureText_OnStationTrackColumn
	{
		get => BeforeDeparture.Text_OnStationTrackColumn;
		set => BeforeDeparture.Text_OnStationTrackColumn = value;
	}
	#endregion

	public TrainInfo_BeforeDeparture()
	{
		RowDefinitions = DefaultRowDefinitions;
		ColumnDefinitions = DTACElementStyles.TimetableColumnWidthCollection;

		Line trainInfoAreaSeparator = DTACElementStyles.HorizontalSeparatorLineStyle();
		TrainInfoArea.HorizontalOptions = LayoutOptions.Start;

		// BeforeDepartureArea
		BeforeDeparture = new(this, "発前");

		Grid.SetColumnSpan(TrainInfoArea, 8);
		this.Add(
			trainInfoAreaSeparator,
			row: 0
		);
		this.Add(
			TrainInfoArea,
			row: 0
		);

		BeforeDeparture.AddToParent();
		BeforeDeparture.SetRow(1);
	}
}
