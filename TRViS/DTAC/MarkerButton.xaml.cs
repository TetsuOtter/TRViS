using CommunityToolkit.Maui.Views;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class MarkerButton : Frame
{
	DTACMarkerViewModel MarkerSettings { get; }
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
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
		if (Shell.Current.CurrentPage is not ViewHost page)
		{
			logger.Warn("Shell.Current.CurrentPage is not ViewHost");
			return;
		}

		if (MarkerSettings.IsToggled)
		{
			logger.Info("MarkerSettings.IsToggled set true -> false");
			MarkerSettings.IsToggled = false;
			return;
		}

		MarkerSettings.IsToggled = true;

		SelectMarkerPopup popup = new(MarkerSettings)
		{
			Anchor = this,
		};

		logger.Info("Showing SelectMarkerPopup");
		await page.ShowPopupAsync(popup);
		logger.Trace("SelectMarkerPopup Shown");
	}
}
