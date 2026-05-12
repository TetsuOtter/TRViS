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
	public partial Mode TabMode { get; set; } = Mode.Hako;

	[ObservableProperty]
	public partial bool IsVerticalViewMode { get; set; } = false;
	[ObservableProperty]
	public partial bool IsHakoMode { get; set; } = true;
	[ObservableProperty]
	public partial bool IsWorkAffixMode { get; set; } = false;
	[ObservableProperty]
	public partial bool IsViewHostVisible { get; set; } = false;

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
