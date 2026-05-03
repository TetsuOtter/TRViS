using System.ComponentModel;

namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Identifies which DTAC tab is currently active.
/// Numeric values intentionally match DTACViewHostViewModel.Mode.
/// </summary>
public enum DTACTabMode
{
	VerticalView = 0,
	Hako = 1,
	WorkAffix = 2,
	None = -1,
}

/// <summary>
/// Provides view host mode information
/// </summary>
public interface IViewHostModeProvider
{
	/// <summary>
	/// Whether the view host is currently visible
	/// </summary>
	bool IsViewHostVisible { get; }

	/// <summary>
	/// Whether vertical view mode is currently active
	/// </summary>
	bool IsVerticalViewMode { get; }

	/// <summary>
	/// The currently active DTAC tab mode.
	/// Defaults to None via default interface implementation so existing fakes compile unchanged.
	/// </summary>
	DTACTabMode TabMode => IsVerticalViewMode ? DTACTabMode.VerticalView : DTACTabMode.None;

	/// <summary>
	/// Whether hako mode is currently active.
	/// </summary>
	bool IsHakoMode => TabMode == DTACTabMode.Hako;

	/// <summary>
	/// Whether work affix mode is currently active.
	/// </summary>
	bool IsWorkAffixMode => TabMode == DTACTabMode.WorkAffix;

	/// <summary>
	/// Fired when a property changes
	/// </summary>
	event PropertyChangedEventHandler? PropertyChanged;
}
