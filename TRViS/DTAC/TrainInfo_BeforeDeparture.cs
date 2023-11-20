using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;

namespace TRViS.DTAC;

public partial class TrainInfo_BeforeDeparture : Grid
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	static readonly RowDefinitionCollection DefaultRowDefinitions = new()
	{
		new RowDefinition(DTACElementStyles.BeforeDeparture_AfterArrive_Height),
		new RowDefinition(DTACElementStyles.BeforeDeparture_AfterArrive_Height)
	};

	#region TrainInfo Area
	readonly HtmlAutoDetectLabel TrainInfoArea = DTACElementStyles.LargeLabelStyle<HtmlAutoDetectLabel>();

	public string TrainInfoText
	{
		get => TrainInfoArea.Text;
		set
		{
			logger.Info("TrainInfoText: {0}", value);
			TrainInfoArea.Text = value;
		}
	}
	#endregion

	#region Before Departure Area
	readonly BeforeDeparture_AfterArrive BeforeDeparture;

	public string BeforeDepartureText
	{
		get => BeforeDeparture.Text;
		set
		{
			logger.Info("BeforeDepartureText: {0}", value);
			BeforeDeparture.Text = value;
		}
	}

	public string BeforeDepartureText_OnStationTrackColumn
	{
		get => BeforeDeparture.Text_OnStationTrackColumn;
		set
		{
			logger.Info("BeforeDepartureText_OnStationTrackColumn: {0}", value);
			BeforeDeparture.Text_OnStationTrackColumn = value;
		}
	}
	#endregion

	readonly Line Separator = DTACElementStyles.HorizontalSeparatorLineStyle();

	public TrainInfo_BeforeDeparture()
	{
		logger.Trace("Creating...");

		RowDefinitions = DefaultRowDefinitions;
		ColumnDefinitions = DTACElementStyles.TimetableColumnWidthCollection;

		TrainInfoArea.HorizontalOptions = LayoutOptions.Start;

		// BeforeDepartureArea
		BeforeDeparture = new(this, "発前", true);

		Grid.SetColumnSpan(TrainInfoArea, 8);
		Add(TrainInfoArea);

		BeforeDeparture.AddToParent();
		BeforeDeparture.SetRow(1);

		Separator.Opacity = 1.0;
		DTACElementStyles.AddHorizontalSeparatorLineStyle(this, Separator, 0);

		logger.Trace("Created");
	}
}
