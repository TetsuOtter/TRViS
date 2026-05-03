namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Controls the DTAC tab mode.
/// Implemented by the adapter layer so the View can request mode changes
/// without directly referencing DTACViewHostViewModel.
/// </summary>
public interface ITabModeController
{
	/// <summary>
	/// Forces the active tab mode to Hako (box view).
	/// Called when a tab button becomes disabled while selected.
	/// </summary>
	void ResetToHako();
}
