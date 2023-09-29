using CommunityToolkit.Maui.Views;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class SelectMarkerPopup : Popup
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public SelectMarkerPopup()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		logger.Trace("Created");
	}

	public SelectMarkerPopup(DTACMarkerViewModel vm) : this()
	{
		logger.Trace("SelectMarkerPopup(DTACMarkerViewModel: {0})", vm);

		BindingContext = vm;

		logger.Trace("Created");
	}

	protected override void OnDismissedByTappingOutsideOfPopup()
	{
		logger.Trace("Processing...");
		base.OnDismissedByTappingOutsideOfPopup();
	}
}
