using Microsoft.JSInterop;

using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.Web.Adapters;

/// <summary>
/// Browser screen-wake-lock backed implementation. Falls back to no-op when the
/// JS module is unavailable.
/// </summary>
public sealed class BrowserWakeLock : IWakeLockController
{
	private readonly IJSRuntime _js;
	private bool _enabled;

	public BrowserWakeLock(IJSRuntime js)
	{
		_js = js;
	}

	public bool IsWakeLockEnabled => _enabled;

	public void EnableWakeLock()
	{
		_enabled = true;
		_ = _js.InvokeVoidAsync("trvisWeb.wakeLock.request");
	}

	public void DisableWakeLock()
	{
		_enabled = false;
		_ = _js.InvokeVoidAsync("trvisWeb.wakeLock.release");
	}
}
