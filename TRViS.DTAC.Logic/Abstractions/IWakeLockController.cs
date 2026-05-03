namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Controls screen wake lock
/// </summary>
public interface IWakeLockController
{
	/// <summary>
	/// Enables the screen wake lock
	/// </summary>
	void EnableWakeLock();

	/// <summary>
	/// Disables the screen wake lock
	/// </summary>
	void DisableWakeLock();

	/// <summary>
	/// Whether wake lock is currently enabled
	/// </summary>
	bool IsWakeLockEnabled { get; }
}
