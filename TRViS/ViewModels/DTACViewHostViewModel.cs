using CommunityToolkit.Mvvm.ComponentModel;
using TRViS.Services;

namespace TRViS.ViewModels;

public partial class DTACViewHostViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public enum Mode
	{
		VerticalView,
		Hako,
		WorkAffix
	}

	[ObservableProperty]
	Mode _TabMode = Mode.Hako;

	[ObservableProperty]
	bool _IsVerticalViewMode = false;
	[ObservableProperty]
	bool _IsHakoMode = true;
	[ObservableProperty]
	bool _IsWorkAffixMode = false;
	[ObservableProperty]
	bool _IsViewHostVisible = false;

	public DTACViewHostViewModel()
	{
		// Subscribe to AppViewModel changes
		var appVm = InstanceManager.AppViewModel;
		appVm.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(AppViewModel.IsDisplayingSearchedTrain))
			{
				OnPropertyChanged(nameof(IsHakoTabVisible));
				logger.Debug("Hako tab visibility changed: {0}", IsHakoTabVisible);
			}
		};
	}

	/// <summary>
	/// Hako tab should be hidden when displaying a searched train (not from current work)
	/// </summary>
	public bool IsHakoTabVisible => !InstanceManager.AppViewModel.IsDisplayingSearchedTrain;

	partial void OnTabModeChanged(Mode value)
	{
		IsVerticalViewMode = (value == Mode.VerticalView);
		IsHakoMode = (value == Mode.Hako);
		IsWorkAffixMode = (value == Mode.WorkAffix);
	}
}
