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

	/// <summary>
	/// 「ハコ」タブの表示可否。検索した列車を表示中 (所定の行路と異なる) は false になり、
	/// 所定列車へ戻ると true に戻る (Issue #197)。ViewHost.xaml の Hako タブが購読する。
	/// </summary>
	[ObservableProperty]
	public partial bool IsHakoTabVisible { get; set; } = true;

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
