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
}
