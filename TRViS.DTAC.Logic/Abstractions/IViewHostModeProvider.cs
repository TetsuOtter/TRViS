using System.ComponentModel;

namespace TRViS.DTAC.Logic.Abstractions;

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
	/// Fired when a property changes
	/// </summary>
	event PropertyChangedEventHandler? PropertyChanged;
}
