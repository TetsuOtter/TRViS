using CommunityToolkit.Mvvm.ComponentModel;

namespace TRViS.ViewModels;

public partial class DTACViewHostViewModel : ObservableObject
{
	public enum Mode
	{
		VerticalView,
		Hako,
		WorkAffix
	}

	public AppViewModel AppViewModel { get; }

	[ObservableProperty]
	Mode _TabMode = Mode.Hako;

	[ObservableProperty]
	bool _IsVerticalViewMode = false;
	[ObservableProperty]
	bool _IsHakoMode = true;
	[ObservableProperty]
	bool _IsWorkAffixMode = false;

	public DTACViewHostViewModel(AppViewModel vm)
	{
		AppViewModel = vm;
	}

	partial void OnTabModeChanged(Mode value)
	{
		IsVerticalViewMode = (value == Mode.VerticalView);
		IsHakoMode = (value == Mode.Hako);
		IsWorkAffixMode = (value == Mode.WorkAffix);
	}
}
