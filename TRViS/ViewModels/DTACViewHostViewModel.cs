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

	[ObservableProperty]
	Mode _TabMode = Mode.VerticalView;

	[ObservableProperty]
	bool _IsVerticalViewMode = true;
	[ObservableProperty]
	bool _IsHakoMode = false;
	[ObservableProperty]
	bool _IsWorkAffixMode = false;

	public DTACViewHostViewModel()
	{
	}

	partial void OnTabModeChanged(Mode value)
	{
		IsVerticalViewMode = (value == Mode.VerticalView);
		IsHakoMode = (value == Mode.Hako);
		IsWorkAffixMode = (value == Mode.WorkAffix);
	}
}
