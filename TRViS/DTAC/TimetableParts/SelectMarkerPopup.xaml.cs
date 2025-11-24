using TRViS.Services;
using TRViS.ViewModels;
using TR.Maui.AnchorPopover;

namespace TRViS.DTAC;

public partial class SelectMarkerPopup : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private IAnchorPopover? _popover;

	public SelectMarkerPopup() : this(InstanceManager.DTACMarkerViewModel) { }

	public SelectMarkerPopup(DTACMarkerViewModel viewModel)
	{
		logger.Trace("Creating...");

		BindingContext = viewModel;

		InitializeComponent();

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		logger.Trace("Created");
	}

	async void OnCloseButtonClicked(object sender, EventArgs e)
	{
		logger.Trace("Closing...");

		if (_popover != null)
		{
			await _popover.DismissAsync();
		}

		logger.Trace("Closed");
	}

	internal void SetPopover(IAnchorPopover popover)
	{
		_popover = popover;
	}
}
