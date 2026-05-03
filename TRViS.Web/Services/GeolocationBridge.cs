using Microsoft.JSInterop;

using TRViS.Services;

namespace TRViS.Web.Services;

/// <summary>
/// Bridges navigator.geolocation -&gt; <see cref="LocationService.SetGpsLocation"/>.
/// JS posts updates back via DotNetObjectReference.
/// </summary>
public sealed class GeolocationBridge : IAsyncDisposable
{
	private readonly IJSRuntime _js;
	private readonly LocationService _locationService;
	private DotNetObjectReference<GeolocationBridge>? _selfRef;
	private bool _watching;

	public GeolocationBridge(IJSRuntime js, LocationService locationService)
	{
		_js = js;
		_locationService = locationService;
	}

	public bool IsWatching => _watching;

	public async Task StartAsync()
	{
		if (_watching) return;
		_selfRef ??= DotNetObjectReference.Create(this);
		await _js.InvokeVoidAsync("trvisWeb.geolocation.start", _selfRef);
		_watching = true;
	}

	public async Task StopAsync()
	{
		if (!_watching) return;
		await _js.InvokeVoidAsync("trvisWeb.geolocation.stop");
		_watching = false;
	}

	[JSInvokable]
	public void OnPosition(double longitude, double latitude, double? accuracy)
	{
		// useAverageDistance=false because we get continuous watchPosition updates
		_locationService.SetGpsLocation(longitude, latitude, accuracy, useAverageDistance: false);
	}

	[JSInvokable]
	public void OnError(string message)
	{
		_locationService.OnGpsListeningFailed(new InvalidOperationException(message));
	}

	public async ValueTask DisposeAsync()
	{
		try { await StopAsync(); } catch { /* ignore */ }
		_selfRef?.Dispose();
	}
}
