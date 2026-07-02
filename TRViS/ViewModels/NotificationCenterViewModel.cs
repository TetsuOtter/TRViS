using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.NetworkSyncService;
using TRViS.Services;

namespace TRViS.ViewModels;

/// <summary>
/// サーバーから受信した通告 (Notification) を管理し、未読の通告をポップアップ表示する
/// ための ViewModel。重複排除・既読 (受領済み) 管理の実体は MAUI 非依存の
/// <see cref="NotificationStore"/> に委譲し、この層は
/// <list type="bullet">
/// <item>WebSocket 受信スレッド → UI スレッドへのマーシャリング</item>
/// <item>表示要求イベント (<see cref="DisplayRequested"/>) の発火</item>
/// <item>「受領」操作のサーバー送信 (<see cref="AcknowledgeAsync"/>)</item>
/// </list>
/// のみを担う。表示 (ポップアップの push) は購読側 (AppShell) が行う。
/// </summary>
public sealed class NotificationCenterViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private readonly NotificationStore _store = new();
	private TRViS.Services.LocationService? _locationService;

	/// <summary>
	/// 未読の通告をポップアップ表示すべきときに発火する。UI スレッド上で発火する。
	/// AppShell が購読してモーダルを push する。
	/// </summary>
	public event EventHandler<NotificationStore.Entry>? DisplayRequested;

	/// <summary>
	/// <see cref="LocationService"/> の通告受信イベントを購読する。
	/// InstanceManager から一度だけ呼ばれる。
	/// </summary>
	public void Subscribe(TRViS.Services.LocationService locationService)
	{
		if (_locationService is not null)
			_locationService.NotificationReceived -= OnNotificationReceived;
		_locationService = locationService;
		_locationService.NotificationReceived += OnNotificationReceived;
	}

	private void OnNotificationReceived(object? sender, NotificationData n)
	{
		logger.Info("NotificationReceived: Id={0}, Title={1}, Priority={2}, Acknowledged={3}",
			n.Id, n.Title, n.Priority, n.Acknowledged);

		// NotificationReceived は WebSocket 受信スレッド (バックグラウンド) から呼ばれる。
		// NotificationStore の更新自体はスレッドセーフだが、DisplayRequested の購読側は
		// UI 操作 (モーダル push) を行うため、状態更新も含めて UI スレッドへ回す。
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var result = _store.Add(n);
			if (result.ShouldDisplay)
				DisplayRequested?.Invoke(this, result.Entry);
		});
	}

	/// <summary>
	/// 通告の「受領」処理。サーバーへ受領を通知し、送信が確定したときにのみローカルで既読にする。
	/// <para>
	/// 戻り値 <c>true</c> は「サーバーへ受領を送信できた (＝既読化した)」ことを表す。
	/// 切断中などで送信できなかった場合は既読化せず <c>false</c> を返す。呼び出し側 (ポップアップ) は
	/// <c>false</c> のときポップアップを閉じず、ユーザーに再試行を促す。これにより
	/// 「受領したつもりだがサーバーに届いていない」サイレントな取りこぼしを防ぐ。
	/// </para>
	/// <para>
	/// <see cref="NotificationStore.Entry.CanAcknowledge"/> が false (Id 無し) の通告は
	/// 受領不可なお知らせのため送信不要で、常に <c>true</c> を返す (ローカル既読のみ)。
	/// </para>
	/// </summary>
	public async Task<bool> AcknowledgeAsync(NotificationStore.Entry entry)
	{
		if (entry.Id is not string id || string.IsNullOrEmpty(id))
		{
			// Id 無し (受領不可) は送信不要。ローカル既読のみで成功扱い。
			logger.Info("Acknowledge notification skipped (no Id)");
			return true;
		}

		logger.Info("Acknowledge notification: Id={0}", id);

#if UI_TEST
		// UI_TEST の seam で注入された通告は実サーバーが存在しないため、
		// 送信を成功扱いにして「受領 → 既読 → 閉じる」の UI 動作を検証できるようにする。
		// 本番の通告 (注入されていない Id) はこの分岐に入らず、必ず実送信を経る。
		if (_testInjectedIds.Contains(id))
		{
			_store.MarkRead(id);
			return true;
		}
#endif

		try
		{
			if (_locationService is null)
				throw new InvalidOperationException("LocationService is not available.");
			await _locationService.AcknowledgeNotificationAsync(id);
		}
		catch (Exception ex)
		{
			// 切断中 (未接続) や送信失敗。既読化せず false を返し、ポップアップを閉じさせない。
			logger.Warn(ex, "Acknowledge send failed: Id={0}", id);
			return false;
		}

		// 送信が確定したときにのみ既読化する (失敗した受領がセッション中に失われないように)。
		_store.MarkRead(id);
		return true;
	}

#if UI_TEST
	// UI_TEST で注入された通告の Id。実サーバーが無いため受領送信を成功扱いにする対象。
	private readonly System.Collections.Generic.HashSet<string> _testInjectedIds = new();

	/// <summary>
	/// UI_TEST 専用: 実サーバー無しで通告受信をシミュレートする。
	/// <see cref="OnNotificationReceived"/> と同じ経路を通す。
	/// </summary>
	/// <param name="n">注入する通告</param>
	/// <param name="fakeAck">
	/// true (既定) のとき、この通告の受領を実サーバー無しで成功扱いにする
	/// (「受領 → 閉じる」の正常系を検証するため)。false のとき seam に登録せず、
	/// 受領は実経路 (未接続なら送信失敗) を通る。受領失敗時にポップアップが閉じない
	/// 異常系を検証するために使う。
	/// </param>
	public void InjectNotificationForTesting(NotificationData n, bool fakeAck = true)
	{
		if (fakeAck && !string.IsNullOrEmpty(n.Id))
			_testInjectedIds.Add(n.Id);
		OnNotificationReceived(this, n);
	}

	/// <summary>
	/// UI_TEST 専用: 保持している通告・既読状態を破棄する。Appium のセッション共有下で
	/// 各テストがクリーンな状態から通告を注入できるようにする (受信済み Id の再表示抑止で
	/// リトライが表示アサーションに失敗するのを防ぐ)。
	/// </summary>
	public void ResetForTesting()
	{
		_store.Clear();
		_testInjectedIds.Clear();
	}
#endif
}
