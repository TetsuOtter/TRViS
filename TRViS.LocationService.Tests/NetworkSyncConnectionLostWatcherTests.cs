using NUnit.Framework;

using TRViS.NetworkSyncService;

namespace TRViS.LocationService.Tests;

// NetworkSyncConnectionLostWatcher の単体テスト (#261)。
// 既存の FakeNetworkSyncService (NetworkSyncServiceBase サブクラス、
// RaiseConnectionClosed / RaiseConnectionFailed を公開) を流用する。
[TestFixture]
public class NetworkSyncConnectionLostWatcherTests
{
	[Test]
	public void Ctor_NullCallback_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => new NetworkSyncConnectionLostWatcher(null!));
	}

	[Test]
	public void ConnectionClosed_OnWatchedService_InvokesCallbackOnce()
	{
		int count = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(() => count++);
		var svc = new FakeNetworkSyncService();
		watcher.Watch(svc);

		svc.RaiseConnectionClosed();

		Assert.That(count, Is.EqualTo(1));
	}

	[Test]
	public void ConnectionFailed_OnWatchedService_InvokesCallbackOnce()
	{
		int count = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(() => count++);
		var svc = new FakeNetworkSyncService();
		watcher.Watch(svc);

		svc.RaiseConnectionFailed();

		Assert.That(count, Is.EqualTo(1));
	}

	[Test]
	public void Rewatch_DifferentService_OldServiceNoLongerInvokesCallback_NewOneDoes()
	{
		int count = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(() => count++);
		var oldSvc = new FakeNetworkSyncService();
		var newSvc = new FakeNetworkSyncService();

		watcher.Watch(oldSvc);
		watcher.Watch(newSvc);

		// 旧サービス (再接続で差し替えられた側) のイベントはコールバックを発火しない。
		oldSvc.RaiseConnectionClosed();
		Assert.That(count, Is.EqualTo(0), "stale service must not trigger the callback");

		// 新サービスのイベントは発火する。
		newSvc.RaiseConnectionClosed();
		Assert.That(count, Is.EqualTo(1));
	}

	[Test]
	public void Clear_ThenFormerlyWatchedServiceRaises_DoesNotInvokeCallback()
	{
		int count = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(() => count++);
		var svc = new FakeNetworkSyncService();

		watcher.Watch(svc);
		watcher.Clear();

		svc.RaiseConnectionClosed();
		svc.RaiseConnectionFailed();

		Assert.Multiple(() =>
		{
			Assert.That(count, Is.EqualTo(0));
			Assert.That(watcher.Watched, Is.Null);
		});
	}

	// 再接続スワップ後に旧サービスが遅延イベントを出しても banner を再点灯させない、
	// かつ watcher は常に最新サービスを追従する、という統合的な振る舞いの確認。
	[Test]
	public void AfterSwap_OnlyLatestServiceDrivesCallback()
	{
		int count = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(() => count++);
		var a = new FakeNetworkSyncService();
		var b = new FakeNetworkSyncService();

		watcher.Watch(a);
		watcher.Watch(b);

		a.RaiseConnectionFailed();   // 旧 (A) -> 無視
		Assert.That(count, Is.EqualTo(0));

		b.RaiseConnectionFailed();   // 最新 (B) -> 発火
		Assert.That(count, Is.EqualTo(1));
		Assert.That(watcher.Watched, Is.SameAs(b));
	}

	[Test]
	public void Rewatch_SameService_DoesNotDoubleSubscribe()
	{
		int count = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(() => count++);
		var svc = new FakeNetworkSyncService();

		watcher.Watch(svc);
		watcher.Watch(svc); // 同一インスタンスの再 Watch は no-op であるべき

		svc.RaiseConnectionClosed();

		Assert.That(count, Is.EqualTo(1), "re-watching the same service must not double-subscribe");
	}

	// --- 自動再接続イベントの中継 (#266) ---

	[Test]
	public void Reconnecting_OnWatchedService_InvokesReconnectingCallback()
	{
		int lost = 0, reconnecting = 0, reconnected = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(
			() => lost++, () => reconnecting++, () => reconnected++);
		var svc = new FakeNetworkSyncService();
		watcher.Watch(svc);

		svc.RaiseReconnecting();

		Assert.Multiple(() =>
		{
			Assert.That(reconnecting, Is.EqualTo(1));
			Assert.That(lost, Is.EqualTo(0));
			Assert.That(reconnected, Is.EqualTo(0));
		});
	}

	[Test]
	public void Reconnected_OnWatchedService_InvokesReconnectedCallback()
	{
		int lost = 0, reconnecting = 0, reconnected = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(
			() => lost++, () => reconnecting++, () => reconnected++);
		var svc = new FakeNetworkSyncService();
		watcher.Watch(svc);

		svc.RaiseReconnected();

		Assert.Multiple(() =>
		{
			Assert.That(reconnected, Is.EqualTo(1));
			Assert.That(reconnecting, Is.EqualTo(0));
			Assert.That(lost, Is.EqualTo(0));
		});
	}

	[Test]
	public void TypicalReconnectCycle_RaisesLostThenReconnectingThenReconnected()
	{
		var order = new List<string>();
		var watcher = new NetworkSyncConnectionLostWatcher(
			() => order.Add("lost"),
			() => order.Add("reconnecting"),
			() => order.Add("reconnected"));
		var svc = new FakeNetworkSyncService();
		watcher.Watch(svc);

		// ReceiveLoopAsync の典型シーケンス: 切断 -> 再接続開始 -> 再接続成功。
		svc.RaiseConnectionClosed();
		svc.RaiseReconnecting();
		svc.RaiseReconnected();

		Assert.That(order, Is.EqualTo(new[] { "lost", "reconnecting", "reconnected" }));
	}

	[Test]
	public void ReconnectCallbacks_AreOptional_NullIsSafe()
	{
		// onReconnecting/onReconnected を渡さない既存呼び出し形でも、サービスが
		// 当該イベントを発火して例外にならないこと。
		var watcher = new NetworkSyncConnectionLostWatcher(() => { });
		var svc = new FakeNetworkSyncService();
		watcher.Watch(svc);

		Assert.DoesNotThrow(() =>
		{
			svc.RaiseReconnecting();
			svc.RaiseReconnected();
		});
	}

	[Test]
	public void AfterSwap_StaleService_ReconnectEvents_DoNotInvokeCallbacks()
	{
		int reconnecting = 0, reconnected = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(
			() => { }, () => reconnecting++, () => reconnected++);
		var oldSvc = new FakeNetworkSyncService();
		var newSvc = new FakeNetworkSyncService();

		watcher.Watch(oldSvc);
		watcher.Watch(newSvc);

		oldSvc.RaiseReconnecting();
		oldSvc.RaiseReconnected();
		Assert.Multiple(() =>
		{
			Assert.That(reconnecting, Is.EqualTo(0), "stale service reconnect must not fire");
			Assert.That(reconnected, Is.EqualTo(0), "stale service reconnect must not fire");
		});

		newSvc.RaiseReconnecting();
		newSvc.RaiseReconnected();
		Assert.Multiple(() =>
		{
			Assert.That(reconnecting, Is.EqualTo(1));
			Assert.That(reconnected, Is.EqualTo(1));
		});
	}

	[Test]
	public void Clear_ThenReconnectEvents_DoNotInvokeCallbacks()
	{
		int reconnecting = 0, reconnected = 0;
		var watcher = new NetworkSyncConnectionLostWatcher(
			() => { }, () => reconnecting++, () => reconnected++);
		var svc = new FakeNetworkSyncService();

		watcher.Watch(svc);
		watcher.Clear();

		svc.RaiseReconnecting();
		svc.RaiseReconnected();

		Assert.Multiple(() =>
		{
			Assert.That(reconnecting, Is.EqualTo(0));
			Assert.That(reconnected, Is.EqualTo(0));
			Assert.That(watcher.Watched, Is.Null);
		});
	}
}
