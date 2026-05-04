using System.ComponentModel;

namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Controls the marker toggle state
/// </summary>
public interface IMarkerToggleController : INotifyPropertyChanged
{
	/// <summary>
	/// Whether the marker toggle is currently on.
	/// </summary>
	bool IsToggled { get; }

	/// <summary>
	/// Resets the toggle state
	/// </summary>
	void ResetToggle();

	/// <summary>
	/// Toggles the current state.
	/// </summary>
	void Toggle();
}
