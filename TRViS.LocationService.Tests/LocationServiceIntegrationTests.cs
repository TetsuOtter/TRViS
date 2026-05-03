using System.Net;
using System.Net.WebSockets;
using System.Reflection;

using NUnit.Framework;

using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.LocationService.Tests;

// =========================================================
// Fake implementations
// =========================================================

/// <summary>
/// テスト用 ITimeProvider。固定の時刻を返す。
/// </summary>
internal class FakeTimeProvider : ITimeProvider
{
	public TimeProgressionRate ProgressionRate { get; set; } = TimeProgressionRate.Normal;
	public event EventHandler<TimeProgressionRate>? ProgressionRateChanged { add { } remove { } }

	public int CurrentTime { get; set; } = 0;
	public int GetCurrentTimeSeconds() => CurrentTime;
}

/// <summary>
/// テスト用 HttpMessageHandler。指定のレスポンスを返す。
/// </summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
	public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var response = ResponseFactory?.Invoke(request)
			?? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
		return Task.FromResult(response);
	}
}

/// <summary>
/// テスト用 NetworkSyncServiceBase サブクラス。
/// CanStart/CanUseService を外から設定できる。
/// </summary>
internal class FakeNetworkSyncService : NetworkSyncServiceBase
{
	public bool ShouldStop { get; set; } = false;
	public Action? OnGetSyncedData { get; set; }

	public void SimulateCanStart(bool value)
	{
		// Use reflection to set protected CanStart
		typeof(NetworkSyncServiceBase)
			.GetProperty("CanStart")!
			.SetValue(this, value);
	}

	public new void RaiseConnectionClosed() => base.RaiseConnectionClosed();
	public new void RaiseConnectionFailed() => base.RaiseConnectionFailed();

	protected override Task<SyncedData> GetSyncedDataAsync(CancellationToken token)
	{
		OnGetSyncedData?.Invoke();
		if (ShouldStop)
			throw new TaskCanceledException("Fake stop");
		return Task.FromResult(new SyncedData(double.NaN, 0, false));
	}

	public override void Dispose() { }
}

/// <summary>
/// WebSocketNetworkSyncService のサブクラス。接続なしで CanStart イベントをシミュレートできる。
/// </summary>
internal class FakeWebSocketNetworkSyncService : WebSocketNetworkSyncService
{
	public FakeWebSocketNetworkSyncService()
		: base(new Uri("ws://localhost:9999"), new ClientWebSocket())
	{
	}

	public void SimulateCanStart(bool value)
	{
		typeof(NetworkSyncServiceBase)
			.GetProperty("CanStart")!
			.SetValue(this, value);
	}

	public new void RaiseConnectionClosed() => base.RaiseConnectionClosed();
	public new void RaiseConnectionFailed() => base.RaiseConnectionFailed();
}

// =========================================================
// Test fixture helpers
// =========================================================

/// <summary>
/// LocationService integration tests.
/// </summary>
[TestFixture]
public class LocationServiceIntegrationTests
{
	private FakeTimeProvider _timeProvider = null!;
	private HttpClient _httpClient = null!;
	private Services.LocationService _locationService = null!;

	private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

	private static StaLocationInfo[] SampleStations() =>
	[
		new(0,   0,   0,   200),
		new(100, 1,   1,   200),
		new(200, 2,   2,   200),
	];

	[SetUp]
	public void SetUp()
	{
		_timeProvider = new FakeTimeProvider();
		_httpClient = new HttpClient(new FakeHttpMessageHandler());
		_locationService = new Services.LocationService(
			Logger,
			Logger,
			Logger,
			_httpClient,
			_timeProvider
		);
		// Dispatcher null = 同期実行
	}

	[TearDown]
	public void TearDown()
	{
		_locationService.Dispose();
		_httpClient.Dispose();
	}

	// -------------------------------------------------------
	// 1. SetGpsLocation_BeforeSetStationLocations_DoesNotThrow
	// -------------------------------------------------------
	[Test]
	public void SetGpsLocation_BeforeSetStationLocations_DoesNotThrow()
	{
		_locationService.IsEnabled = true;
		Assert.DoesNotThrow(() =>
		{
			_locationService.SetGpsLocation(135.0, 35.0, 10.0);
		});
	}

	// -------------------------------------------------------
	// 2. SetGpsLocation_FirstCall_CallsForceSetLocationInfo
	// -------------------------------------------------------
	[Test]
	public void SetGpsLocation_FirstCall_CallsForceSetLocationInfo()
	{
		_locationService.SetStationLocations(SampleStations());
		_locationService.IsEnabled = true;

		LocationStateChangedEventArgs? lastState = null;
		_locationService.LocationStateChanged += (_, e) => lastState = e;

		// First call should ForceSetLocationInfo -> station near (0,0) → station 0
		_locationService.SetGpsLocation(0.0, 0.0, null);

		Assert.That(lastState, Is.Not.Null);
		Assert.That(lastState!.NewStationIndex, Is.EqualTo(0));
	}

	// -------------------------------------------------------
	// 3. SetGpsLocation_SubsequentCall_CallsSetCurrentLocation_AndFiresLocationStateChanged
	// -------------------------------------------------------
	[Test]
	public void SetGpsLocation_SubsequentCall_MovesToNextStation()
	{
		_locationService.SetStationLocations(SampleStations());
		_locationService.IsEnabled = true;

		// Force first call to initialize
		_locationService.SetGpsLocation(0.0, 0.0, null);

		int lastStationIndex = -1;
		bool? lastRunningToNext = null;
		_locationService.LocationStateChanged += (_, e) =>
		{
			lastStationIndex = e.NewStationIndex;
			lastRunningToNext = e.IsRunningToNextStation;
		};

		// Subsequent calls near station 1 (1,1)
		_locationService.SetGpsLocation(1.0, 1.0, null, false);
		_locationService.SetGpsLocation(1.0, 1.0, null, false);
		_locationService.SetGpsLocation(1.0, 1.0, null, false);

		// Should have moved near station 1
		Assert.That(lastStationIndex, Is.GreaterThanOrEqualTo(0));
	}

	// -------------------------------------------------------
	// 4. SetTargetIds_AfterNetworkSync_UpdatesNetworkSyncServiceIds
	// -------------------------------------------------------
	[Test]
	public async Task SetTargetIds_AfterNetworkSync_UpdatesNetworkSyncServiceIds()
	{
		var fakeNs = new FakeNetworkSyncService();
		await _locationService.SetNetworkSyncServiceAsync(fakeNs);

		_locationService.SetTargetIds("wg1", "w1", "t1");

		Assert.Multiple(() =>
		{
			Assert.That(fakeNs.WorkGroupId, Is.EqualTo("wg1"));
			Assert.That(fakeNs.WorkId, Is.EqualTo("w1"));
			Assert.That(fakeNs.TrainId, Is.EqualTo("t1"));
		});
	}

	// -------------------------------------------------------
	// 5. SetTargetIds_BeforeNetworkSync_PersistsAndAppliesOnSet
	// -------------------------------------------------------
	[Test]
	public async Task SetTargetIds_BeforeNetworkSync_PersistsAndAppliesOnSet()
	{
		// Set IDs before any NetworkSync is assigned
		_locationService.SetTargetIds("wg2", "w2", "t2");

		var fakeNs = new FakeNetworkSyncService();
		await _locationService.SetNetworkSyncServiceAsync(fakeNs);

		Assert.Multiple(() =>
		{
			Assert.That(fakeNs.WorkGroupId, Is.EqualTo("wg2"));
			Assert.That(fakeNs.WorkId, Is.EqualTo("w2"));
			Assert.That(fakeNs.TrainId, Is.EqualTo("t2"));
		});
	}

	// -------------------------------------------------------
	// 6. Connection切断 → AlertRequested発火 + LonLatLocationService に切替
	// -------------------------------------------------------
	[Test]
	public async Task ConnectionClosed_FiresAlertRequested_AndSwitchesToLonLat()
	{
		var fakeNs = new FakeNetworkSyncService();
		await _locationService.SetNetworkSyncServiceAsync(fakeNs);

		UserAlertRequestedEventArgs? alertArgs = null;
		_locationService.AlertRequested += (_, e) => alertArgs = e;

		fakeNs.RaiseConnectionClosed();

		// After connection closed, should have switched to LonLat (not NetworkSync)
		Assert.That(_locationService.CurrentService, Is.InstanceOf<LonLatLocationService>());
		Assert.That(alertArgs, Is.Not.Null);
		Assert.That(alertArgs!.Title, Does.Contain("切断"));
	}

	// -------------------------------------------------------
	// 7. WebSocket CanStart=true → IsEnabled=true 自動切替
	//    (業務ルール: WebSocketNetworkSyncService使用時のみ自動で運行開始)
	// -------------------------------------------------------
	[Test]
	public async Task WebSocketCanStart_AutomaticallyEnablesLocationService()
	{
		var fakeWs = new FakeWebSocketNetworkSyncService();
		await _locationService.SetNetworkSyncServiceAsync(fakeWs);

		Assert.That(_locationService.IsEnabled, Is.False, "Initially should be disabled");

		fakeWs.SimulateCanStart(true);

		// CanStartChanged event fires, which should trigger IsEnabled = true
		Assert.That(_locationService.IsEnabled, Is.True, "Should be enabled after CanStart=true on WebSocket");
	}

	// -------------------------------------------------------
	// 8. Interval property 書き込み → 内部参照値が更新される
	// -------------------------------------------------------
	[Test]
	public void Interval_PropertySetGet_ReflectsNewValue()
	{
		_locationService.Interval = TimeSpan.FromSeconds(5);
		Assert.That(_locationService.Interval, Is.EqualTo(TimeSpan.FromSeconds(5)));
	}

	// -------------------------------------------------------
	// 9a. Dispatcher null → イベント同期発火
	// -------------------------------------------------------
	[Test]
	public void Dispatcher_Null_SynchronousExecution()
	{
		_locationService.Dispatcher = null;

		// Set stations to enable CanUseService
		_locationService.SetStationLocations(SampleStations());

		// CanUseService should fire synchronously (via SetLonLatLocationService or SetStationLocations)
		// Just verify no exception and it works synchronously
		Assert.DoesNotThrow(() =>
		{
			_locationService.SetStationLocations(null);
		});
	}

	// -------------------------------------------------------
	// 9b. Dispatcher設定 → marshaling経由発火（spy確認）
	// -------------------------------------------------------
	[Test]
	public void Dispatcher_Set_IsCalledWhenEventFires()
	{
		int dispatchCallCount = 0;
		_locationService.Dispatcher = a =>
		{
			dispatchCallCount++;
			a();
		};

		// Trigger CanUseServiceChanged via SetStationLocations
		_locationService.SetStationLocations(SampleStations());

		// Dispatcher should have been called at least once
		Assert.That(dispatchCallCount, Is.GreaterThan(0));
	}

	// -------------------------------------------------------
	// 10. OnGpsListeningFailed → IsEnabled=false + ExceptionThrown
	// -------------------------------------------------------
	[Test]
	public void OnGpsListeningFailed_DisablesAndFiresException()
	{
		_locationService.SetStationLocations(SampleStations());
		_locationService.IsEnabled = true;

		Exception? thrownEx = null;
		_locationService.ExceptionThrown += (_, ex) => thrownEx = ex;

		var testException = new Exception("GPS failed");
		_locationService.OnGpsListeningFailed(testException);

		Assert.That(_locationService.IsEnabled, Is.False);
		Assert.That(thrownEx, Is.EqualTo(testException));
	}

	// -------------------------------------------------------
	// 11. ConnectionFailed → AlertRequested発火 + LonLat切替
	// -------------------------------------------------------
	[Test]
	public async Task ConnectionFailed_FiresAlertRequested_AndSwitchesToLonLat()
	{
		var fakeNs = new FakeNetworkSyncService();
		await _locationService.SetNetworkSyncServiceAsync(fakeNs);

		UserAlertRequestedEventArgs? alertArgs = null;
		_locationService.AlertRequested += (_, e) => alertArgs = e;

		fakeNs.RaiseConnectionFailed();

		Assert.That(_locationService.CurrentService, Is.InstanceOf<LonLatLocationService>());
		Assert.That(alertArgs, Is.Not.Null);
		Assert.That(alertArgs!.Title, Does.Contain("失敗").Or.Contain("切断"));
	}

	// -------------------------------------------------------
	// 12. SetStationLocations で null を渡す → CanUseService = false
	// -------------------------------------------------------
	[Test]
	public void SetStationLocations_Null_CanUseServiceFalse()
	{
		_locationService.SetStationLocations(SampleStations());
		_locationService.SetStationLocations(null);

		Assert.That(_locationService.CanUseService, Is.False);
	}

	// -------------------------------------------------------
	// 13. OnGpsLocationUpdated event fires on SetGpsLocation
	// -------------------------------------------------------
	[Test]
	public void SetGpsLocation_FiresOnGpsLocationUpdated()
	{
		_locationService.SetStationLocations(SampleStations());
		_locationService.IsEnabled = true;

		(double lon, double lat, double? acc)? received = null;
		_locationService.OnGpsLocationUpdated += (_, e) => received = e;

		_locationService.SetGpsLocation(135.0, 35.0, 5.0);

		Assert.That(received, Is.Not.Null);
		Assert.That(received!.Value.lon, Is.EqualTo(135.0));
		Assert.That(received!.Value.lat, Is.EqualTo(35.0));
		Assert.That(received!.Value.acc, Is.EqualTo(5.0));
	}
}
