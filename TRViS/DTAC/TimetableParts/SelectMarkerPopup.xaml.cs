using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class SelectMarkerPopup : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private IPagePopupHost? _host;

	public SelectMarkerPopup() : this(Adapters.PresenterFactory.GetRawMarkerViewModel()) { }

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

		if (_host != null)
		{
			await _host.DismissAsync();
		}

		logger.Trace("Closed");
	}

	internal void SetPopupHost(IPagePopupHost host)
	{
		_host = host;
	}
}
