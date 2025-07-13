using CommunityToolkit.Maui.Views;

using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class SelectMarkerPopup : Popup
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public SelectMarkerPopup() : this(InstanceManager.DTACMarkerViewModel) { }

	public SelectMarkerPopup(DTACMarkerViewModel viewModel)
	{
		logger.Trace("Creating...");

		BindingContext = viewModel;

		InitializeComponent();

		DTACElementStyles.Instance.DefaultBGColor.Apply(this, ColorProperty);

		logger.Trace("Created");
	}

	async void OnCloseButtonClicked(object sender, EventArgs e)
	{
		logger.Trace("Closing...");

		await CloseAsync();

		logger.Trace("Closed");
	}
}
