using System.Net.WebSockets;

using NLog;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.IO;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Web.Adapters;

namespace TRViS.Web.Services;

/// <summary>
/// Singleton state shared across pages: holds the loaded JSON, the LocationService,
/// and lazily builds the VerticalStyle presenter.
/// </summary>
public sealed class DtacAppState : IDisposable
{
	public LocationService LocationService { get; }
	public WebLocationServiceAdapter LocationAdapter { get; }
	public BrowserWakeLock WakeLock { get; }
	public SimpleEasterEgg EasterEgg { get; } = new();
	public StaticViewHostMode ViewHostMode { get; } = new();
	public SimpleMarkerToggle MarkerToggle { get; } = new();
	public SystemClock Clock { get; } = new();
	public ConsoleCrashLogger CrashLogger { get; } = new();

	public LoaderJson? Loader { get; private set; }
	public string? LoadedSourceLabel { get; private set; }

	public TrainData? SelectedTrain { get; private set; }
	public string? SelectedAffectDate { get; private set; }

	private VerticalStylePagePresenter? _verticalPresenter;

	public event Action? StateChanged;

	public DtacAppState(LocationService locationService, BrowserWakeLock wakeLock)
	{
		LocationService = locationService;
		WakeLock = wakeLock;
		LocationAdapter = new WebLocationServiceAdapter(locationService);
	}

	public VerticalStylePagePresenter GetOrCreateVerticalPresenter()
	{
		_verticalPresenter ??= new VerticalStylePagePresenter(
			LocationAdapter,
			WakeLock,
			EasterEgg,
			ViewHostMode,
			MarkerToggle,
			CrashLogger,
			Clock);
		return _verticalPresenter;
	}

	public void LoadJson(byte[] bytes, string label)
	{
		Loader?.Dispose();
		Loader = LoaderJson.InitFromBytes(bytes);
		LoadedSourceLabel = label;
		SelectedTrain = null;
		SelectedAffectDate = null;
		StateChanged?.Invoke();
	}

	public void SelectTrain(TrainData? train, string? affectDate)
	{
		SelectedTrain = train;
		SelectedAffectDate = affectDate;
		_verticalPresenter?.OnSelectedTrainDataChanged(train, affectDate);
		LocationService.SetTargetIds(null, null, train?.Id);
		StateChanged?.Invoke();
	}

	public async Task SetHttpNetworkSyncAsync(Uri uri, CancellationToken ct = default)
	{
		await LocationService.SetNetworkSyncServiceAsync(uri, ct);
		StateChanged?.Invoke();
	}

	public async Task SetWebSocketNetworkSyncAsync(Uri uri, CancellationToken ct = default)
	{
		var ws = await NetworkSyncServiceUtil.CreateFromWebSocketAsync(uri, cancellationToken: ct);
		await LocationService.SetNetworkSyncServiceAsync(ws);
		StateChanged?.Invoke();
	}

	public void SwitchToGpsMode()
	{
		LocationService.SetLonLatLocationService();
		StateChanged?.Invoke();
	}

	public void Dispose()
	{
		_verticalPresenter?.Dispose();
		Loader?.Dispose();
		LocationService.Dispose();
	}
}
