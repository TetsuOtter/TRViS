using DependencyPropertyGenerator;

using TRViS.DTAC.TimetableParts;
using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<double>("FontSize_Large")]
public partial class TimetableHeader : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public TimetableHeader()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		ColumnDefinitions = InstanceManager.DTACViewHostViewModel.VerticalStyleColumnDefinitionsProvider.TimetableRowColumnDefinitions;
		InstanceManager.DTACViewHostViewModel.VerticalStyleColumnDefinitionsProvider.ViewWidthModeChanged += (sender, e) =>
		{
			OnViewWidthModeChanged();
		};
		OnViewWidthModeChanged();

		logger.Trace("Created");
	}

	private void OnViewWidthModeChanged()
	{
		VerticalTimetableRowColumnDefinitionsProvider provider = InstanceManager.DTACViewHostViewModel.VerticalStyleColumnDefinitionsProvider;
		RunTimeLabel.IsVisible = provider.IsRunTimeColumnVisible;
		LimitLabel.IsVisible = LimitSeparator.IsVisible = provider.IsSpeedLimitColumnVisible;
		RemarksLabel.IsVisible = provider.IsRemarksColumnVisible;
		MarkerBtn.IsVisible = provider.IsMarkerColumnVisible;
	}
}
