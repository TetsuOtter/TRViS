using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Core;
using TRViS.LocationService.Abstractions;
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

	// 区間連動の小型再表示 (RefreshRedisplay) が扱う現在位置・現在列車の駅順。
	// いずれも UI スレッド上でのみ読み書きする (LocationStateChanged はバックグラウンド
	// スレッドから来るため、OnLocationStateChanged / SetCurrentTrainStations で
	// MainThread.BeginInvokeOnMainThread 経由に揃えている)。
	private int _currentStationIndex = -1;
	private bool _isRunningToNextStation;
	private IReadOnlyList<StationRef> _stations = System.Array.Empty<StationRef>();

	// 現在バナー表示中の全 Key (区間連動の再表示 + 受領必須の初回小型表示の両方)。
	// RefreshRedisplay がこれと最新の visible 集合との差分だけを発火する。UI スレッド専有。
	private readonly HashSet<string> _shownBannerKeys = new();

	// 受領必須の初回小型表示 (compact-initial) のうち、まだ受領されていないものの Id。
	// 区間内かどうかに関わらず表示対象とし、受領されたら RefreshRedisplay で自動的に
	// 取り除かれる (区間内ならそのまま再表示バナーへ切り替わり、区間外なら消える)。UI スレッド専有。
	private readonly HashSet<string> _pendingCompactKeys = new();

	// Id を持つ通告の最新 Entry を Id で引けるようにしたもの。NotificationStore は
	// Id 単体でのルックアップ API を公開していないため、RefreshRedisplay が
	// pending-compact な Key から Entry を解決するために保持する。UI スレッド専有。
	private readonly Dictionary<string, NotificationStore.Entry> _entriesById = new();

	/// <summary>
	/// 未読の通告をポップアップ表示すべきときに発火する。UI スレッド上で発火する。
	/// AppShell が購読してモーダルを push する。
	/// </summary>
	public event EventHandler<NotificationStore.Entry>? DisplayRequested;

	/// <summary>
	/// 小型バナーの表示を要求する。entry.IsRead が false のとき受領必須の初回小型表示
	/// (受領ボタンを出す)、true のとき受領済みの区間連動再表示 (受領ボタンなし)。
	/// UI スレッド上で発火する。
	/// </summary>
	public event EventHandler<NotificationStore.Entry>? BannerRequested;

	/// <summary>
	/// 指定 Id の小型バナーを消すことを要求する (区間を抜けた等)。UI スレッド上で発火する。
	/// </summary>
	public event EventHandler<string>? BannerDismissed;

	/// <summary>
	/// 指定 Id の通告が削除されたときに発火する (サーバーからの削除指示、または UI_TEST の
	/// 削除 seam)。開いている大型ポップアップのうち該当 Id のものだけを自己クローズさせ、
	/// AppShell の表示待ちキューから該当 Id のみを取り除くために使う。表示中の小型バナーは
	/// <see cref="RemoveNotification"/> が併せて発火する <see cref="BannerDismissed"/> で消える。
	/// UI スレッド上で発火する。
	/// </summary>
	public event EventHandler<string>? NotificationRemoved;

	// 現在表示中の通告ポップアップ (拡大表示) の数。AppShell のキュー直列化により通常は
	// 同時に 1 つだが、close→次表示の切り替わりの一瞬だけ増減が重なる可能性に備えてカウンタに
	// している。UI スレッド専有。
	private int _popupVisibleCount;

	/// <summary>
	/// 通告ポップアップ (拡大表示) が現在表示中かどうか。ViewHost が
	/// (再生成後の) 初期状態を復元する際に参照する。
	/// </summary>
	public bool IsPopupVisible => _popupVisibleCount > 0;

	/// <summary>
	/// <see cref="IsPopupVisible"/> が変化したときに発火する。ViewHost が購読し、ポップアップ
	/// 表示中は小型バナーの受領ボタンの点滅を一時停止する (最小化/閉じたら再開)。
	/// UI スレッド上で発火する。
	/// </summary>
	public event EventHandler<bool>? PopupVisibilityChanged;

	/// <summary>
	/// <see cref="NotificationPopupPage"/> が自身の表示/非表示を通知する。
	/// </summary>
	public void NotifyPopupVisibilityChanged(bool isVisible)
	{
		_popupVisibleCount = Math.Max(0, _popupVisibleCount + (isVisible ? 1 : -1));
		PopupVisibilityChanged?.Invoke(this, IsPopupVisible);
	}

	/// <summary>
	/// <see cref="LocationService"/> の通告受信イベント・位置変化イベントを購読する。
	/// InstanceManager から一度だけ呼ばれる。
	/// </summary>
	public void Subscribe(TRViS.Services.LocationService locationService)
	{
		if (_locationService is not null)
		{
			_locationService.NotificationReceived -= OnNotificationReceived;
			_locationService.NotificationDeleteRequested -= OnNotificationDeleteRequested;
			_locationService.LocationStateChanged -= OnLocationStateChanged;
			_locationService.NetworkConnectionLost -= OnNetworkConnectionLost;
		}
		_locationService = locationService;
		_locationService.NotificationReceived += OnNotificationReceived;
		_locationService.NotificationDeleteRequested += OnNotificationDeleteRequested;
		_locationService.LocationStateChanged += OnLocationStateChanged;
		_locationService.NetworkConnectionLost += OnNetworkConnectionLost;
	}

	// WebSocket/HTTP 接続が切断されると、未受領のまま残った通告はサーバーとの整合性が
	// 取れなくなる (受領しても届かない、再配信されるかも不明) ため、保持している通告を
	// すべて破棄する。再接続後にサーバーが再配信すれば通常のフローで復元される。
	private void OnNetworkConnectionLost(object? sender, EventArgs e)
		=> MainThread.BeginInvokeOnMainThread(ClearAll);

	/// <summary>
	/// 保持しているすべての通告 (未受領・既読・バナー表示中を問わず) を破棄する。
	/// 表示中の小型バナーは <see cref="BannerDismissed"/> の通常経路で個別に消え、開いている
	/// 大型ポップアップ (<see cref="RootPages.NotificationPopupPage"/>) は <see cref="Cleared"/>
	/// を購読して自分で閉じる。AppShell が持つ未表示の表示待ちキューも
	/// <see cref="Cleared"/> を購読して空にする。UI スレッド上でのみ呼ぶこと。
	/// </summary>
	public void ClearAll()
	{
		_store.Clear();
		_pendingCompactKeys.Clear();
		_entriesById.Clear();

		foreach (var key in _shownBannerKeys.ToArray())
			BannerDismissed?.Invoke(this, key);
		_shownBannerKeys.Clear();

		Cleared?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// <see cref="ClearAll"/> で保持中の通告がすべて破棄されたときに発火する。開いている
	/// 大型ポップアップの自己クローズ、AppShell の表示待ちキューのクリアに使う。
	/// UI スレッド上で発火する。
	/// </summary>
	public event EventHandler? Cleared;

	private void OnNotificationReceived(object? sender, NotificationData n)
	{
		logger.Info("NotificationReceived: Id={0}, Title={1}, Priority={2}, Acknowledged={3}",
			n.Id, n.Title, n.Priority, n.Acknowledged);

		// NotificationReceived は WebSocket 受信スレッド (バックグラウンド) から呼ばれる。
		// NotificationStore の更新自体はスレッドセーフだが、DisplayRequested / BannerRequested の
		// 購読側は UI 操作 (モーダル push・バナー表示) を行うため、状態更新も含めて UI スレッドへ回す。
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var result = _store.Add(n);
			if (!string.IsNullOrEmpty(result.Entry.Id))
				_entriesById[result.Entry.Id] = result.Entry;

			if (result.ShouldDisplay)
			{
				if (result.Entry.CompactDisplay)
				{
					// Id 付きは pending-compact として登録し RefreshRedisplay に発火を委ねる。
					// これにより受領後の自動非表示 (RefreshRedisplay 参照) が効くようになる。
					// Id 無し (受領不可の一過性通告) は Key で追跡できないため直接発火する。
					if (result.Entry.Id is string id && !string.IsNullOrEmpty(id))
					{
						_pendingCompactKeys.Add(id);
						RefreshRedisplay();
					}
					else
					{
						BannerRequested?.Invoke(this, result.Entry);
					}
				}
				else
				{
					DisplayRequested?.Invoke(this, result.Entry);
				}
			}
			else if (result.Entry.IsRead && result.Entry.HasRedisplayTarget)
			{
				// 受領済み・区間指定付きの通告は ShouldDisplay=false (初回ポップアップ対象外) だが、
				// 列車遷移直後にサーバーから受領済み一覧として再送された場合、現在位置が既に
				// その区間内であれば LocationStateChanged が改めて発火するとは限らない
				// (位置が変わらなければ何も起きない) ため、ここで明示的に再評価しないと
				// バナーが表示されないまま取り残される。
				RefreshRedisplay();
			}
		});
	}

	/// <summary>
	/// サーバーからの通告削除指示 (<see cref="DeleteNotificationCommand"/>) を受けて、指定 Id の
	/// 通告を破棄する。WebSocket 受信スレッドから呼ばれ得るため UI スレッドへ回す。
	/// </summary>
	private void OnNotificationDeleteRequested(object? sender, DeleteNotificationCommand cmd)
	{
		logger.Info("NotificationDeleteRequested: Id={0}", cmd.Id);
		MainThread.BeginInvokeOnMainThread(() => RemoveNotification(cmd.Id));
	}

	/// <summary>
	/// 指定 Id の通告を、受領済み/未受領・表示中/未表示を問わず破棄する。
	/// <see cref="NotificationStore"/> から削除し、追跡中の状態
	/// (<see cref="_pendingCompactKeys"/>/<see cref="_entriesById"/>) からも取り除いた上で、
	/// 表示中の小型バナーがあれば <see cref="BannerDismissed"/> で消し、開いている大型ポップアップ
	/// (該当 Id のみ)・AppShell の表示待ちキュー (該当 Id のみ) には <see cref="NotificationRemoved"/>
	/// で通知する。UI スレッド上でのみ呼ぶこと。
	/// </summary>
	private void RemoveNotification(string? id)
	{
		if (string.IsNullOrEmpty(id))
			return;

		bool existed = _store.Remove(id);
		_pendingCompactKeys.Remove(id);
		_entriesById.Remove(id);

		if (_shownBannerKeys.Remove(id))
			BannerDismissed?.Invoke(this, id);

		if (existed)
			NotificationRemoved?.Invoke(this, id);
	}

	/// <summary>
	/// 位置情報 (現在駅・進行方向) の変化を受けて、区間連動の小型再表示を再評価する。
	/// LocationStateChanged はバックグラウンドスレッドから発火し得るため UI スレッドへ回す。
	/// </summary>
	private void OnLocationStateChanged(object? sender, LocationStateChangedEventArgs e)
	{
		_currentStationIndex = e.NewStationIndex;
		_isRunningToNextStation = e.IsRunningToNextStation;
		MainThread.BeginInvokeOnMainThread(RefreshRedisplay);
	}

	/// <summary>
	/// 現在選択中の列車の駅順リスト (info-row 除外済み、LocationService と同じインデックス空間) を
	/// 設定する。AppViewModel が選択列車の切り替え時に呼ぶ。
	/// </summary>
	public void SetCurrentTrainStations(IReadOnlyList<StationRef> stations)
	{
		_stations = stations;
		MainThread.BeginInvokeOnMainThread(RefreshRedisplay);
	}

	/// <summary>
	/// 「いま表示すべきバナー」の集合を再計算し、前回との差分だけ
	/// <see cref="BannerRequested"/> / <see cref="BannerDismissed"/> を発火する。
	/// 対象は次の 2 種類の和集合:
	/// <list type="bullet">
	/// <item>受領済み・区間/駅指定付きの通告のうち、現在駅から見て区間内にあるもの
	///   (<see cref="NotificationRedisplayEvaluator"/> による判定)</item>
	/// <item>受領必須の初回小型表示 (compact-initial) のうち、まだ受領されていないもの
	///   (<see cref="_pendingCompactKeys"/>。区間の内外を問わず表示し続け、受領された
	///   時点でここから除外することで自動的に消える/再表示バナーへ切り替わる)</item>
	/// </list>
	/// UI スレッド上でのみ呼ぶこと (フィールドはすべて UI スレッド専有)。
	/// </summary>
	private void RefreshRedisplay()
	{
		var candidates = _store.GetRedisplayCandidates();

		var visible = new HashSet<string>();
		if (_stations.Count > 0 && _currentStationIndex >= 0)
		{
			var targets = candidates.Select(c => new RedisplayTarget(c.Id!, c.SectionStartStation, c.SectionEndStation, c.StationsBefore));
			visible.UnionWith(NotificationRedisplayEvaluator.EvaluateVisibleKeys(_stations, _currentStationIndex, _isRunningToNextStation, targets));
		}

		// 受領済みになった compact-initial は追跡対象から外す。区間内ならこの後
		// visible に既に含まれ続けるため、キー自体は「表示中」のまま変わらない。
		// ただし受領ボタンの要不要が変わった (未受領→受領済み) ので、この遷移分は
		// justAcknowledged としてマークし、下で「表示済みでも」再発火の対象にする。
		var justAcknowledged = new HashSet<string>();
		foreach (var key in _pendingCompactKeys)
		{
			if (_store.IsRead(key))
				justAcknowledged.Add(key);
		}
		_pendingCompactKeys.ExceptWith(justAcknowledged);
		visible.UnionWith(_pendingCompactKeys);

		foreach (var key in visible)
		{
			bool alreadyShown = _shownBannerKeys.Contains(key);
			if (alreadyShown && !justAcknowledged.Contains(key))
				continue;
			var entry = candidates.FirstOrDefault(c => c.Id == key)
				?? (_entriesById.TryGetValue(key, out var pending) ? pending : null);
			if (entry is not null)
				BannerRequested?.Invoke(this, entry);
		}

		foreach (var key in _shownBannerKeys)
		{
			if (!visible.Contains(key))
				BannerDismissed?.Invoke(this, key);
		}

		_shownBannerKeys.Clear();
		foreach (var key in visible)
			_shownBannerKeys.Add(key);
	}

	/// <summary>
	/// いま表示すべき小型バナー (<see cref="BannerRequested"/> 済みで未 <see cref="BannerDismissed"/> の
	/// もの) の一覧を返す。ViewHost は破棄・再生成され得る (Android) ため、購読の有無に依らず
	/// OnAppearing でこのスナップショットを引いてバナー表示を復元できるようにする。
	/// <see cref="_shownBannerKeys"/> をそのまま <see cref="RefreshRedisplay"/> と同じ手順
	/// (redisplay candidates → <see cref="_entriesById"/> の順) で Entry へ解決するだけで、
	/// RefreshRedisplay 自体の判定・発火ロジックに変更は無い。UI スレッド上でのみ呼ぶこと。
	/// </summary>
	public IReadOnlyList<NotificationStore.Entry> GetCurrentBanners()
	{
		var candidates = _store.GetRedisplayCandidates();

		List<NotificationStore.Entry> result = [];
		foreach (var key in _shownBannerKeys)
		{
			var entry = candidates.FirstOrDefault(c => c.Id == key)
				?? (_entriesById.TryGetValue(key, out var pending) ? pending : null);
			if (entry is not null)
				result.Add(entry);
		}
		return result;
	}

	/// <summary>
	/// 大型ポップアップの「最小化」から呼ばれる。指定の通告を小型バナー表示に切り替える。
	/// Id を持つ通告は (受領済みかどうかに関わらず) <see cref="_pendingCompactKeys"/> 経由で
	/// 通常のバナーライフサイクル (<see cref="RefreshRedisplay"/> による受領後・区間退出時の
	/// 自動非表示) に乗せる。既に区間連動等で表示中の場合は冪等 (状態変化なし)。
	/// Id 無し (受領不可) の通告は追跡できないため、<see cref="OnNotificationReceived"/> の
	/// Id 無し分岐と同様に <see cref="BannerRequested"/> を直接発火するだけに留める。
	/// UI スレッド上でのみ呼ぶこと。
	/// </summary>
	public void RequestBannerDisplay(NotificationStore.Entry entry)
	{
		if (entry.Id is string id && !string.IsNullOrEmpty(id))
		{
			_entriesById[id] = entry;
			if (!entry.IsRead)
				_pendingCompactKeys.Add(id);
			RefreshRedisplay();
		}
		else
		{
			BannerRequested?.Invoke(this, entry);
		}
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
			// 受領直後に対象の区間が既にアクティブなら、そのまま再表示バナーへ切り替える。
			MainThread.BeginInvokeOnMainThread(RefreshRedisplay);
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
		// 受領直後に対象の区間が既にアクティブなら、そのまま再表示バナーへ切り替える。
		MainThread.BeginInvokeOnMainThread(RefreshRedisplay);
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
	/// UI_TEST 専用: 実サーバー無しで通告削除 (<see cref="DeleteNotificationCommand"/>) をシミュレートする。
	/// <see cref="RemoveNotification"/> と同じ経路 (本番の削除ハンドラーが呼ぶものと同一) を通す。
	/// </summary>
	public void DeleteNotificationForTesting(string id)
	{
		_testInjectedIds.Remove(id);
		RemoveNotification(id);
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
		_shownBannerKeys.Clear();
		_pendingCompactKeys.Clear();
		_entriesById.Clear();
		_currentStationIndex = -1;
		_stations = System.Array.Empty<StationRef>();
	}
#endif
}
