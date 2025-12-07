using TRViS.Services;
using TRViS.ViewModels;
using TR.Maui.AnchorPopover;

namespace TRViS.DTAC;

public partial class MarkerButton : Border
{
	DTACMarkerViewModel MarkerSettings { get; }
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public MarkerButton()
	{
		logger.Trace("Creating...");

		MarkerSettings = InstanceManager.DTACMarkerViewModel;
		BindingContext = MarkerSettings;
		InitializeComponent();

		logger.Trace("Created");
	}

	async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
	{
		try
		{
			if (MarkerSettings.IsToggled)
			{
				logger.Info("MarkerSettings.IsToggled set true -> false");
				MarkerSettings.IsToggled = false;
				return;
			}

			MarkerSettings.IsToggled = true;

			SelectMarkerPopup popup = new(MarkerSettings);
			var popover = AnchorPopover.Create();
			popup.SetPopover(popover);

			var options = new PopoverOptions
			{
				PreferredWidth = 240,
				PreferredHeight = 360,
				DismissOnTapOutside = true
			};

			logger.Info("Showing SelectMarkerPopup");
			await popover.ShowAsync(popup, this, options);
			logger.Trace("SelectMarkerPopup Shown");
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "MarkerButton.Tap");
			await Util.ExitWithAlert(ex);
		}
	}
}
