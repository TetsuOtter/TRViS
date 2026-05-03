using System.ComponentModel;

namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Provides access to easter egg settings
/// </summary>
public interface IEasterEggSettings
{
	/// <summary>
	/// Whether to keep screen on when running
	/// </summary>
	bool KeepScreenOnWhenRunning { get; }

	/// <summary>
	/// Whether to show map when device is in landscape mode
	/// </summary>
	bool ShowMapWhenLandscape { get; }

	/// <summary>
	/// Fired when a property changes
	/// </summary>
	event PropertyChangedEventHandler? PropertyChanged;
}
