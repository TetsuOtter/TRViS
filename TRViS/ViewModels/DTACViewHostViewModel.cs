using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.DTAC;

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
	Mode _TabMode = Mode.Hako;

	[ObservableProperty]
	bool _IsVerticalViewMode = false;
	[ObservableProperty]
	bool _IsHakoMode = true;
	[ObservableProperty]
	bool _IsWorkAffixMode = false;
	[ObservableProperty]
	bool _IsViewHostVisible = false;

	public DTACRowDefinitionsProvider RowDefinitionsProvider { get; } = new();
	public DTACColumnDefinitionsProvider ColumnDefinitionsProvider { get; } = new();

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
