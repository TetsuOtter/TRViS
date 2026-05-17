using TRViS.Services;
using TRViS.ViewModels;
using TRViS.Utils;

namespace TRViS.DTAC;

public partial class MarkerButton : Border
{
	DTACMarkerViewModel MarkerSettings { get; }
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public MarkerButton()
	{
		logger.Trace("Creating...");

		MarkerSettings = Adapters.PresenterFactory.GetRawMarkerViewModel();
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

			// TR.Maui.AnchorPopover crashes on Windows MAUI 10 (#273); the
			// popup is now an in-page overlay owned by the hosting ViewHost.
			ViewHost? host = ViewHost.GetHostFor(this);
			if (host is null)
			{
				logger.Warn("MarkerButton: hosting ViewHost not found — cannot show SelectMarkerPopup");
				return;
			}

			await host.ShowSelectMarkerPopupAsync();
			logger.Trace("SelectMarkerPopup dismissed");
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			Adapters.PresenterFactory.GetCrashLogger().Log(ex, "MarkerButton.Tap");
			await Util.ExitWithAlertAsync(ex);
		}
	}
}
