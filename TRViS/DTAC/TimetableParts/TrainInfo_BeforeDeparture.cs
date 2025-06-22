using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.Services;

namespace TRViS.DTAC;

public partial class TrainInfo_BeforeDeparture : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	static readonly RowDefinitionCollection DefaultRowDefinitions = new()
	{
		new RowDefinition(DTACElementStyles.BeforeDeparture_AfterArrive_Height),
		new RowDefinition(DTACElementStyles.BeforeDeparture_AfterArrive_Height)
	};

	#region TrainInfo Area
	readonly HtmlAutoDetectLabel TrainInfoArea = DTACElementStyles.LargeHtmlAutoDetectLabelStyle<HtmlAutoDetectLabel>();

	public string TrainInfoText
	{
		get => TrainInfoArea.Text ?? string.Empty;
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
	#endregion

	readonly Line Separator = DTACElementStyles.HorizontalSeparatorLineStyle();

	public TrainInfo_BeforeDeparture()
	{
		logger.Trace("Creating...");

		RowDefinitions = DefaultRowDefinitions;
		ColumnDefinitions = InstanceManager.DTACViewHostViewModel.VerticalStyleColumnDefinitionsProvider.TrainInfoBeforeDepartureColumnDefinitions;

		TrainInfoArea.HorizontalOptions = LayoutOptions.Start;

		// BeforeDepartureArea
		BeforeDeparture = new(this, "発前", true);

		Add(TrainInfoArea);

		BeforeDeparture.AddToParent();
		BeforeDeparture.SetRow(1);

		DTACElementStyles.AddHorizontalSeparatorLineStyle(this, Separator, 0);

		logger.Trace("Created");
	}
}
