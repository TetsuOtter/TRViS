using CommunityToolkit.Maui.Views;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class SelectMarkerPopup : Popup
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public SelectMarkerPopup() : this(InstanceManager.DTACMarkerViewModel) { }

	public SelectMarkerPopup(DTACMarkerViewModel viewModel)
	{
		logger.Trace("Creating...");

		BindingContext = viewModel;

		InitializeComponent();

		logger.Trace("Created");
	}
}
