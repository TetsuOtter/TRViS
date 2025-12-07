namespace TRViS.Services;

/// <summary>
/// Service interface for controlling screen wake lock.
/// </summary>
public interface IScreenWakeLockService
{
	/// <summary>
	/// Enables the screen wake lock to prevent the screen from turning off.
	/// </summary>
	void EnableWakeLock();

	/// <summary>
	/// Disables the screen wake lock, allowing the screen to turn off normally.
	/// </summary>
	void DisableWakeLock();

	/// <summary>
	/// Gets whether wake lock is currently enabled.
	/// </summary>
	bool IsWakeLockEnabled { get; }
}
