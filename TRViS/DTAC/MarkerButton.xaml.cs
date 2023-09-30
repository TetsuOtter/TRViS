using CommunityToolkit.Maui.Views;

using DependencyPropertyGenerator;

using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<DTACMarkerViewModel>("MarkerSettings")]
public partial class MarkerButton : Frame
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public MarkerButton()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		logger.Trace("Created");
	}

	partial void OnMarkerSettingsChanged(DTACMarkerViewModel? newValue)
	{
		logger.Trace("OnMarkerSettingsChanged({0})", newValue?.GetType().Name ?? "null");
		BindingContext = newValue;
	}

	async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
	{
		if (Shell.Current.CurrentPage is not ViewHost page || MarkerSettings is null)
		{
			logger.Warn("Shell.Current.CurrentPage is not ViewHost or MarkerSettings is null");
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
