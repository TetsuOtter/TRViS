using TRViS.DTAC.Logic.Abstractions;
using TRViS.Services;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps IScreenWakeLockService to implement IWakeLockController.
/// </summary>
internal class WakeLockAdapter : IWakeLockController
{
	private readonly IScreenWakeLockService _wakeLockService;

	public WakeLockAdapter(IScreenWakeLockService wakeLockService)
	{
		_wakeLockService = wakeLockService ?? throw new ArgumentNullException(nameof(wakeLockService));
	}

	public bool IsWakeLockEnabled => _wakeLockService.IsWakeLockEnabled;

	public void EnableWakeLock() => _wakeLockService.EnableWakeLock();

	public void DisableWakeLock() => _wakeLockService.DisableWakeLock();
}
