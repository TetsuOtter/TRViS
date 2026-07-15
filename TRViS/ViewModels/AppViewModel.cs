using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Core;
using TRViS.IO;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.ViewModels;

/// <summary>
/// WebSocket サーバー接続の表示用ステータス (#266)。AppBar のステータス表示
/// (緑丸 / 赤丸 / ぐるぐる) を駆動する。WebSocket 以外 / 未ロード時は
/// <see cref="None"/> で表示自体を隠す。
/// </summary>
public enum ServerConnectionStatus
{
	/// <summary>WebSocket ローダーではない (ファイル等) / 未ロード -> 非表示。</summary>
	None,
	/// <summary>接続中 / 自動再接続試行中 -> ぐるぐる表示。</summary>
	Connecting,
	/// <summary>接続済み -> 緑丸。</summary>
	Connected,
	/// <summary>接続断 (再接続待ち / 再接続失敗) -> 赤丸。</summary>
	Disconnected,
}

public partial class AppViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public TimetableSelectionManager SelectionManager { get; } = new();

	/// <summary>
	/// サーバーから受信した通告 (Notification) の管理・ポップアップ表示要求を担う ViewModel。
	/// AppShell が <see cref="NotificationCenterViewModel.DisplayRequested"/> を購読して
	/// ポップアップを表示する。
	/// </summary>
	public NotificationCenterViewModel NotificationCenter { get; } = new();

	[ObservableProperty]
	public partial ILoader? Loader { get; set; }

	// Human-readable label for the current Loader's source. Set atomically alongside
	// Loader via SetLoader() so the Home info card cannot momentarily show a stale
	// source between the two assignments. Cleared automatically when Loader becomes null.
	[ObservableProperty]
	public partial string? LoaderSourceLabel { get; set; }

	/// <summary>
	/// Atomically replaces <see cref="Loader"/> and <see cref="LoaderSourceLabel"/>.
	/// Call sites should prefer this over assigning Loader directly so the Home info
	/// card metadata stays in sync with the active loader.
	/// </summary>
	public void SetLoader(ILoader? loader, string? sourceLabel)
	{
		LoaderSourceLabel = sourceLabel;
		Loader = loader;
	}

	// #311: 次の OnLoaderChanged 呼び出し 1 回だけ、選択状態をハードリセットせず
	// TimetableSelectionManager.ReconnectLoader (Id 一致時の維持) に回すためのフラグ。
	private bool _preserveSelectionOnNextLoaderChange;

	/// <summary>
	/// WS再接続 (#311) 用の Loader 差し替え。<see cref="SetLoader"/> と異なり、
	/// WorkGroup/Work/TrainData の選択状態をハードリセットしない。時刻表が
	/// 再配信され Id が一致すれば選択は維持され、一致しなければ未選択に戻る
	/// (<see cref="TimetableSelectionManager.ReconnectLoader"/> 参照)。
	/// </summary>
	public void SetLoaderForReconnect(ILoader? loader, string? sourceLabel)
	{
		LoaderSourceLabel = sourceLabel;
		_preserveSelectionOnNextLoaderChange = true;
		Loader = loader;
	}

	// #311: WS再接続で Loader を差し替えた際に、直前の運行状態 (位置情報 ON/OFF)
	// を記憶しておき、再配信された時刻表で WorkGroupId/WorkId/TrainId が全て
	// 一致したときだけ復元する (OnTimetableUpdated 参照)。
	private (string? WorkGroupId, string? WorkId, string? TrainId, bool WasLocationEnabled)? _pendingReconnectLocationRestore;

	/// <summary>
	/// WebSocket 切断検知の直後 (<c>MarkServerConnectionLost</c>、位置情報サービスが
	/// GPS フォールバックで IsEnabled を強制 OFF する前) に呼び、その時点の選択 Id と
	/// 位置情報サービスの有効状態を記録する。再接続ボタン押下時点では既に IsEnabled が
	/// false になっているため、ここより後で捕まえても復元できない。
	/// </summary>
	internal void RememberSelectionForReconnect(bool isLocationEnabled)
	{
		_pendingReconnectLocationRestore = (
			SelectionManager.SelectedWorkGroup?.Id,
			SelectionManager.SelectedWork?.Id,
			SelectionManager.SelectedTrainData?.Id,
			isLocationEnabled
		);
	}

	/// <summary>
	/// サーバーから受信した最新のダイヤ情報 (ダイヤ名・説明など)。未受信／非接続時は null。
	/// 接続情報画面で読み取り専用表示するために購読される。
	/// </summary>
	[ObservableProperty]
	public partial DiagramInfo? CurrentDiagramInfo { get; set; }

	/// <summary>
	/// サーバーから受信した最新のサーバー情報 (名前・アイコン等)。未受信／非接続時は null。
	/// Home 画面の接続情報カードがアイコン表示のために購読する。
	/// </summary>
	[ObservableProperty]
	public partial ServerInfo? CurrentServerInfo { get; set; }

	/// <summary>
	/// 現在の <see cref="WebSocketNetworkSyncService"/> ローダーがサーバーとの接続を
	/// 失っているか。接続断時に Home 画面が「サーバー接続中」のまま表示され続ける問題
	/// (#261) を解消するため、Home 画面はこの値を購読して切断表示と再接続ボタンを出す。
	/// 切断後もキャッシュ済みデータは <see cref="Loader"/> から読めるため Loader 自体は
	/// 置き換えない。WebSocket 以外のローダーに切り替わった時点で false に戻る
	/// (<see cref="OnLoaderChanged"/>)。
	/// </summary>
	[ObservableProperty]
	public partial bool IsServerConnectionLost { get; set; }

	/// <summary>
	/// WebSocket が接続断後、自動再接続を試行中か (#266)。サービスの
	/// <see cref="WebSocketNetworkSyncService"/> 内部再接続ループ
	/// (Reconnecting/Reconnected) を <see cref="NetworkSyncConnectionLostWatcher"/>
	/// 経由で反映する。AppBar のぐるぐる表示を駆動するためだけの一時状態。
	/// </summary>
	[ObservableProperty]
	public partial bool IsServerReconnecting { get; set; }

	/// <summary>
	/// AppBar のステータス表示 (#266) を駆動する派生プロパティ。
	/// <see cref="Loader"/> / <see cref="IsServerConnectionLost"/> /
	/// <see cref="IsServerReconnecting"/> から算出され、それらが変化したときに
	/// PropertyChanged が発火する (各 On*Changed フック参照)。
	/// 冷間起動で Loader が null の間は再接続フラグに関わらず必ず
	/// <see cref="ServerConnectionStatus.None"/>。
	/// </summary>
	public ServerConnectionStatus ServerConnectionStatus
	{
		get
		{
			if (Loader is not WebSocketNetworkSyncService)
				return ServerConnectionStatus.None;
			if (IsServerReconnecting)
				return ServerConnectionStatus.Connecting;
			if (IsServerConnectionLost)
				return ServerConnectionStatus.Disconnected;
			return ServerConnectionStatus.Connected;
		}
	}

	partial void OnIsServerConnectionLostChanged(bool value)
	{
		OnPropertyChanged(nameof(ServerConnectionStatus));
		OnPropertyChanged(nameof(IsTrainSearchAvailable));
	}

	partial void OnIsServerReconnectingChanged(bool value)
	{
		OnPropertyChanged(nameof(ServerConnectionStatus));
		OnPropertyChanged(nameof(IsTrainSearchAvailable));
	}

	/// <summary>
	/// Raised after a server-driven load (HTTP / WebSocket TRViS.LocalServers
	/// integration) has set the loader and committed a WorkGroup selection, to
	/// request that the UI jump straight to the timetable instead of leaving the
	/// user on the Home picker. StartHomePage subscribes and performs the actual
	/// navigation (it owns navigation + modal lifecycle; raising an event here
	/// avoids doing Shell navigation from the AppLink handler while the
	/// ConnectServerDialog modal may still be on the stack).
	/// </summary>
	public event EventHandler? AutoNavigateToTimetableRequested;

	/// <summary>
	/// Latched intent backing <see cref="AutoNavigateToTimetableRequested"/>.
	/// The event alone is fire-and-forget: a cold-start deeplink
	/// (App handles a <c>trvis://…path=http…</c> AppLink while Shell is still
	/// navigating to StartHomePage) can raise the request before StartHomePage
	/// has subscribed, losing it and stranding the user on the Home picker.
	/// AppViewModel always exists, so the intent is stored here and StartHomePage
	/// also consumes it on OnAppearing — covering the race regardless of
	/// subscribe-vs-raise ordering.
	/// </summary>
	public bool AutoNavigateToTimetablePending { get; private set; }

	public void ConsumeAutoNavigateToTimetablePending()
		=> AutoNavigateToTimetablePending = false;

	internal void RequestAutoNavigateToTimetable()
	{
		AutoNavigateToTimetablePending = true;
		// Still raise the event for the warm path (StartHomePage already
		// subscribed) so navigation happens immediately rather than waiting
		// for the next OnAppearing.
		AutoNavigateToTimetableRequested?.Invoke(this, EventArgs.Empty);
	}

	public IReadOnlyList<WorkGroup>? WorkGroupList => SelectionManager.WorkGroupList;
	public IReadOnlyList<Work>? WorkList => SelectionManager.WorkList;
	public IReadOnlyList<TrainData>? OrderedTrainDataList => SelectionManager.OrderedTrainDataList;

	public WorkGroup? SelectedWorkGroup
	{
		get => SelectionManager.SelectedWorkGroup;
		set => SelectionManager.SelectedWorkGroup = value;
	}

	public Work? SelectedWork
	{
		get => SelectionManager.SelectedWork;
		set => SelectionManager.SelectedWork = value;
	}

	public TrainData? SelectedTrainData
	{
		get => SelectionManager.SelectedTrainData;
		set => SelectionManager.SelectedTrainData = value;
	}

	// ================================================================
	// 列車検索 (Issue #197): 検索して選択した列車の行路 (WorkGroup/Work) へ
	// 完全に切り替える。ヘッダの行路番号も含め、通常の行路選択と同じ扱いになる
	// (サーバー起点の SelectTrain コマンドと同じ ID ルックアップパターン、
	// OnTrainSelectionRequested 参照)。
	// ================================================================

	/// <summary>
	/// 列車検索が使える状態かどうか。QuickSwitchPopup の検索タブの表示可否に使う。
	/// WebSocket サーバー接続時はサーバーが列車検索に対応し、かつ接続中であることを要求する
	/// (オフライン中は検索できないため)。JSON/SQLite などローカルファイルを読み込んでいる
	/// 場合は、読み込み済みデータの中から検索できるため、ローダーが存在すれば常に利用可能。
	/// </summary>
	public bool IsTrainSearchAvailable
		=> Loader is WebSocketNetworkSyncService ws
			? ws.IsFeatureSupported(ServerFeatureIds.TrainSearch) && ServerConnectionStatus == ServerConnectionStatus.Connected
			: Loader is not null;

	/// <summary>
	/// 列番で列車を検索する。<see cref="IsTrainSearchAvailable"/> が true のときのみ有効。
	/// WebSocket 接続時はサーバーに問い合わせ、JSON/SQLite などローカルファイル読み込み時は
	/// 読み込み済みデータの中から <see cref="ILoader"/> 経由で検索する。
	/// </summary>
	public Task<IReadOnlyList<TrainSearchResult>> SearchTrainAsync(
		string trainNumber, TrainSearchMatchMode matchMode = TrainSearchMatchMode.Prefix, System.Threading.CancellationToken cancellationToken = default)
	{
		if (Loader is WebSocketNetworkSyncService ws)
			return ws.SearchTrainAsync(trainNumber, matchMode, cancellationToken);

		if (Loader is null)
			throw new InvalidOperationException("Train search requires loaded data.");

		return Task.FromResult(SearchTrainLocal(Loader, trainNumber, matchMode));
	}

	/// <summary>
	/// 検索候補の完全な時刻表を取得する (2 段階目)。切替先の行路の列車データを
	/// キャッシュへ確実に反映させるため、<see cref="SwitchToSearchedTrain"/> の前に呼ぶ。
	/// ローカルローダーの場合は既に読み込み済みのデータから同期的に取得する。
	/// </summary>
	public Task<TrainData?> FetchSearchedTrainTimetableAsync(
		TrainSearchResult result, System.Threading.CancellationToken cancellationToken = default)
	{
		if (Loader is WebSocketNetworkSyncService ws)
			return ws.FetchSearchedTrainTimetableAsync(result, cancellationToken);

		if (Loader is null)
			throw new InvalidOperationException("Train timetable fetch requires loaded data.");

		if (string.IsNullOrEmpty(result.TrainId))
			throw new ArgumentException("TrainSearchResult.TrainId must not be empty.", nameof(result));

		return Task.FromResult(Loader.GetTrainData(result.TrainId));
	}

	/// <summary>
	/// ローカルローダー (JSON/SQLite/サンプルデータ) の読み込み済みデータから列番で列車を検索する。
	/// マッチ方式はサーバー側 (<c>ReferenceNetworkSyncServer.MatchesTrainNumber</c>) と同じ
	/// OrdinalIgnoreCase の前方一致/中間一致/完全一致。
	/// </summary>
	private static IReadOnlyList<TrainSearchResult> SearchTrainLocal(
		ILoader loader, string trainNumber, TrainSearchMatchMode matchMode)
	{
		if (string.IsNullOrEmpty(trainNumber))
			return [];

		List<TrainSearchResult> results = [];
		foreach (WorkGroup workGroup in loader.GetWorkGroupList())
		{
			foreach (Work work in loader.GetWorkList(workGroup.Id))
			{
				foreach (TrainData train in loader.GetTrainDataList(work.Id))
				{
					if (string.IsNullOrEmpty(train.TrainNumber)
						|| !MatchesTrainNumber(train.TrainNumber, trainNumber, matchMode))
						continue;

					// GetTrainDataList (SQLite) omits Rows for performance, so re-fetch the
					// full record (only for actual matches) to get the start/end station
					// display. LoaderJson/SampleDataLoader already populate Rows in
					// GetTrainDataList, so this is a cheap redundant lookup there — but
					// unconditional keeps both loader kinds on the exact same code path.
					TrainData? full = loader.GetTrainData(train.Id);
					TimetableRow? firstRow = full?.Rows?.FirstOrDefault(static r => !r.IsInfoRow);
					TimetableRow? lastRow = full?.Rows?.LastOrDefault(static r => !r.IsInfoRow);

					results.Add(new TrainSearchResult(
						WorkGroupId: workGroup.Id,
						WorkId: work.Id,
						TrainId: train.Id,
						TrainNumber: train.TrainNumber,
						WorkName: work.Name,
						Direction: train.Direction.ToInt(),
						StartStationName: firstRow?.StationName,
						StartTime: (firstRow?.DepartureTime ?? firstRow?.ArriveTime)?.GetTimeString(),
						EndStationName: lastRow?.StationName,
						EndTime: (lastRow?.ArriveTime ?? lastRow?.DepartureTime)?.GetTimeString()
					));
				}
			}
		}
		return results;
	}

	private static bool MatchesTrainNumber(string trainNumber, string query, TrainSearchMatchMode matchMode)
		=> matchMode switch
		{
			TrainSearchMatchMode.Contains => trainNumber.Contains(query, StringComparison.OrdinalIgnoreCase),
			TrainSearchMatchMode.Exact => string.Equals(trainNumber, query, StringComparison.OrdinalIgnoreCase),
			_ => trainNumber.StartsWith(query, StringComparison.OrdinalIgnoreCase),
		};

	/// <summary>
	/// 検索して選択した列車の行路へ完全に切り替える。WorkGroupId/WorkId/TrainId を
	/// 既存のリストから ID で解決し、SelectionManager の選択を差し替える。
	/// 該当 ID が (まだ) キャッシュに存在しない場合は該当階層の切替をスキップする
	/// (OnTrainSelectionRequested と同じ挙動)。
	/// </summary>
	public void SwitchToSearchedTrain(string? workGroupId, string? workId, string? trainId)
	{
		if (workGroupId is not null)
		{
			var wg = SelectionManager.WorkGroupList?.FirstOrDefault(w => w.Id == workGroupId);
			if (wg is not null && SelectionManager.SelectedWorkGroup?.Id != wg.Id)
				SelectionManager.SelectedWorkGroup = wg;
		}

		if (workId is not null)
		{
			var work = SelectionManager.WorkList?.FirstOrDefault(w => w.Id == workId);
			if (work is not null && SelectionManager.SelectedWork?.Id != work.Id)
				SelectionManager.SelectedWork = work;
		}

		if (trainId is not null)
		{
			var train = SelectionManager.OrderedTrainDataList?.FirstOrDefault(t => t.Id == trainId);
			if (train is not null && SelectionManager.SelectedTrainData?.Id != train.Id)
				SelectionManager.SelectedTrainData = train;
		}
	}

	bool _IsBgAppIconVisible = true;
	public bool IsBgAppIconVisible
	{
		get => _IsBgAppIconVisible;
		set
		{
			if (_IsBgAppIconVisible == value)
				return;
			// 不正利用・誤認防止のため、ライトモード時はアイコン背景を強制表示する。
			if (CurrentAppTheme == AppTheme.Light && value == false)
				return;
			SetProperty(ref _IsBgAppIconVisible, value);
		}
	}

	[ObservableProperty]
	public partial double WindowHeight { get; set; }

	[ObservableProperty]
	public partial double WindowWidth { get; set; }

	public event EventHandler<ValueChangedEventArgs<AppTheme>>? CurrentAppThemeChanged;
	AppTheme _SystemAppTheme;
	public AppTheme SystemAppTheme => _SystemAppTheme;
	AppTheme _CurrentAppTheme;
	public AppTheme CurrentAppTheme
	{
		get => _CurrentAppTheme;
		set
		{
			if (value == AppTheme.Unspecified)
				value = _SystemAppTheme;

			if (_CurrentAppTheme == value)
				return;

			AppTheme tmp = _CurrentAppTheme;
			_CurrentAppTheme = value;
			CurrentAppThemeChanged?.Invoke(this, new(tmp, value));
			// 不正利用・誤認防止のため、ライトモード時はアイコン背景を強制表示する。
			if (value == AppTheme.Light)
				IsBgAppIconVisible = true;
		}
	}

	[JsonSourceGenerationOptions(WriteIndented = false)]
	[JsonSerializable(typeof(List<string>))]
	internal partial class StringListJsonSourceGenerationContext : JsonSerializerContext
	{
	}

	public AppViewModel()
	{
		SelectionManager.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
		SelectionManager.PropertyChanged += OnSelectionManagerPropertyChangedForNotificationCenter;
		// 起動時点で既に選択済みの列車があれば (通常は無いが) 反映しておく。
		PushSelectedTrainStationsToNotificationCenter();

		if (Application.Current is not null)
		{
			_CurrentAppTheme = Application.Current.RequestedTheme;

			// does not fire -> https://github.com/dotnet/maui/pull/11199
			// will be resolved with net8
			Application.Current.RequestedThemeChanged += (s, e) =>
			{
				_SystemAppTheme = e.RequestedTheme;

				if (Application.Current.UserAppTheme == AppTheme.Unspecified)
					CurrentAppTheme = e.RequestedTheme;
			};
		}

		_ExternalResourceUrlHistory = AppPreferenceService.GetFromJson(AppPreferenceKeys.ExternalResourceUrlHistory, [], out _, StringListJsonSourceGenerationContext.Default.ListString);
	}

	/// <summary>
	/// サーバーからホーム画面への遷移要求を受信したときに発火する。
	/// AppShell が購読して MainThread 上でナビゲーションを実行する。
	/// WebSocket 受信スレッドから呼ばれるため、UI 操作は購読側で MainThread に dispatch する。
	/// </summary>
	public event EventHandler? NavigateToHomeRequested;

	/// <summary>
	/// サーバーから OpenTimetable コマンドを受信し、列車選択を適用した後に発火する。
	/// StartHomePage (ホーム画面表示中) が購読して D-TAC へ遷移し、
	/// ViewHost が購読して時刻表タブへ切り替える。
	/// WebSocket 受信スレッドから呼ばれるため、UI 操作は購読側で MainThread に dispatch する。
	/// </summary>
	public event EventHandler? OpenTimetableViewRequested;

	/// <summary>
	/// D-TAC への遷移が未処理であることを表す latch。
	/// StartHomePage が消費して <see cref="HomeGridView.NavigateToDTACAsync"/> を実行する。
	/// </summary>
	public bool OpenTimetableNavigationPending { get; private set; }

	/// <summary>
	/// D-TAC の時刻表タブへの切り替えが未処理であることを表す latch。
	/// ViewHost が消費して TabMode を VerticalView に設定する。
	/// StartHomePage とは独立しており、ナビゲーション後に ViewHost.OnAppearing で確認する。
	/// </summary>
	public bool OpenTimetableTabSwitchPending { get; private set; }

	public void ConsumeOpenTimetableNavigationPending() => OpenTimetableNavigationPending = false;

	/// <summary>
	/// <see cref="OpenTimetableTabSwitchPending"/> フラグを消費する。消費できた場合 true を返す。
	/// </summary>
	public bool ConsumeOpenTimetableTabSwitchPending()
	{
		if (!OpenTimetableTabSwitchPending) return false;
		OpenTimetableTabSwitchPending = false;
		return true;
	}

	/// <summary>
	/// 直前に <see cref="NotificationCenter"/> へ駅順を反映した際の選択列車 Id。
	/// 「表示中の列車から別の列車への遷移」だけを検出するために保持する
	/// (初回選択 (null → 列車) では、まだ何も表示していないため破棄不要)。
	/// </summary>
	string? _lastNotifiedTrainId;

	/// <summary>
	/// 選択列車が別の列車へ切り替わったときに、保持中の通告をすべて破棄し、その駅順を
	/// <see cref="NotificationCenter"/> へ反映する。別の列車の通告を破棄せず表示し続けると
	/// 実際とは異なる列車の通告が残ってしまうため、切り替え時にまず <see cref="NotificationCenterViewModel.ClearAll"/>
	/// で破棄し、切り替え後にサーバーから送信される通告で更新されるのを待つ。
	/// 初回選択 (未選択 → 列車) および Home 復帰時の Loader クリア (列車 → 未選択)
	/// はまだ「別の列車」が表示されていない/されなくなっただけなので対象外。
	/// <see cref="SelectionManager"/> は選択列車が変わると <see cref="SelectionManager.SelectedTrainData"/>
	/// で PropertyChanged を発火するので、それをフィルタして拾う。
	/// </summary>
	void OnSelectionManagerPropertyChangedForNotificationCenter(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(SelectedTrainData))
		{
			string? newTrainId = SelectedTrainData?.Id;
			// null を経由する遷移 (Home 復帰時の Loader クリア等) は「表示中の列車から
			// 別の列車への遷移」ではないため対象外。実際の列車切り替え (NextTrainButton /
			// SelectTrainCommand / 検索) は SelectedTrainData を null を挟まず直接 A→B へ
			// 設定するため、ここを非 null 同士の差分に限定しても実切り替えの検出漏れはない。
			if (_lastNotifiedTrainId is not null && newTrainId is not null && _lastNotifiedTrainId != newTrainId)
			{
				// SelectedTrainData の PropertyChanged はサーバー起点 (OnTrainSelectionRequested) の
				// 場合バックグラウンドスレッドから発火し得るため、UI スレッド専有の ClearAll はここで
				// 明示的にメインスレッドへ回す。
				MainThread.BeginInvokeOnMainThread(NotificationCenter.ClearAll);
			}
			_lastNotifiedTrainId = newTrainId;
			PushSelectedTrainStationsToNotificationCenter();
		}
	}

	/// <summary>
	/// 現在の <see cref="SelectedTrainData"/> の駅順 (info-row 除外済み) を
	/// <see cref="NotificationCenterViewModel.SetCurrentTrainStations"/> へ渡す。
	/// LocationService が組み立てる駅位置情報の配列 (info-row 除外) と同じインデックス空間に
	/// なるよう、ここでも同じフィルタ (IsInfoRow 除外) を適用する。
	/// </summary>
	void PushSelectedTrainStationsToNotificationCenter()
	{
		var rows = SelectedTrainData?.Rows;
		var stations = rows is null
			? System.Array.Empty<StationRef>()
			: rows.Where(r => !r.IsInfoRow)
				.Select(r => new StationRef(r.StationId, r.StationName))
				.ToArray();
		NotificationCenter.SetCurrentTrainStations(stations);
	}

	internal void SubscribeToLocationService(TRViS.Services.LocationService locationService)
	{
		locationService.TimetableUpdated += OnTimetableUpdated;
		locationService.TrainSelectionRequested += OnTrainSelectionRequested;
		locationService.HeaderColorChangeRequested += OnHeaderColorChangeRequested;
		locationService.TimeFormatChangeRequested += OnTimeFormatChangeRequested;
		locationService.DiagramInfoUpdated += OnDiagramInfoUpdated;
		locationService.ServerInfoUpdated += OnServerInfoUpdated;
		locationService.NavigateToHomeRequested += OnNavigateToHomeRequested;
		locationService.OpenTimetableRequested += OnOpenTimetableRequested;
		// 通告 (Notification) は NotificationCenter が購読し、未読をポップアップ表示する
		// (AppShell が DisplayRequested を購読)。OperationCommandReceived は
		// LocationService 側で受信される。OperationCommand のうち位置情報 ON/OFF は
		// LocationService が直接適用し、運行開始/終了そのものは
		// LocationService.OperationStartRequested 経由で DTAC.Adapters.LocationServiceAdapter
		// → VerticalStylePagePresenter に委譲される。
		NotificationCenter.Subscribe(locationService);
	}

	void OnNavigateToHomeRequested(object? sender, EventArgs _)
	{
		logger.Info("OnNavigateToHomeRequested");
		NavigateToHomeRequested?.Invoke(this, EventArgs.Empty);
	}

	void OnOpenTimetableRequested(object? sender, OpenTimetableCommand cmd)
	{
		logger.Info("OnOpenTimetableRequested: WorkGroupId={0}, WorkId={1}, TrainId={2}",
			cmd.WorkGroupId, cmd.WorkId, cmd.TrainId);

		// 列車選択を適用 (SelectTrain と同じ階層ロジック)
		if (cmd.WorkGroupId is not null)
		{
			var wg = SelectionManager.WorkGroupList?.FirstOrDefault(w => w.Id == cmd.WorkGroupId);
			if (wg is not null && SelectionManager.SelectedWorkGroup?.Id != wg.Id)
				SelectionManager.SelectedWorkGroup = wg;
		}
		if (cmd.WorkId is not null)
		{
			var work = SelectionManager.WorkList?.FirstOrDefault(w => w.Id == cmd.WorkId);
			if (work is not null && SelectionManager.SelectedWork?.Id != work.Id)
				SelectionManager.SelectedWork = work;
		}
		if (cmd.TrainId is not null)
		{
			var train = SelectionManager.OrderedTrainDataList?.FirstOrDefault(t => t.Id == cmd.TrainId);
			if (train is not null && SelectionManager.SelectedTrainData?.Id != train.Id)
				SelectionManager.SelectedTrainData = train;
		}

		OpenTimetableNavigationPending = true;
		OpenTimetableTabSwitchPending = true;
		OpenTimetableViewRequested?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// サーバーからダイヤ情報を受信した際に最新値を保持する。
	/// WebSocket 受信スレッドから呼ばれるため、UI 反映側 (View) で
	/// メインスレッドへのマーシャリングを行う。
	/// </summary>
	void OnDiagramInfoUpdated(object? sender, DiagramInfo info)
	{
		logger.Info("DiagramInfoUpdated: Id={0}, Name={1}", info.Id, info.Name);
		CurrentDiagramInfo = info;
	}

	/// <summary>
	/// サーバーからサーバー情報 (名前・アイコン等) を受信した際に最新値を保持する。
	/// WebSocket 受信スレッドから呼ばれるため、UI 反映側 (View) でメインスレッドへの
	/// マーシャリングを行う。
	/// </summary>
	void OnServerInfoUpdated(object? sender, ServerInfo info)
	{
		logger.Info("ServerInfoUpdated: Name={0}", info.Name);
		CurrentServerInfo = info;
	}

	partial void OnLoaderChanged(ILoader? value)
	{
		if (_preserveSelectionOnNextLoaderChange)
		{
			_preserveSelectionOnNextLoaderChange = false;
			SelectionManager.ReconnectLoader(value);
		}
		else
		{
			SelectionManager.Loader = value;
			// #311: 再接続以外の経路 (新規ファイルオープン等) でハードリセットされた
			// 場合、直前の切断時に取っていた位置情報復元スナップショットは無意味な
			// ので捨てる。残したままだと、後で無関係な TimetableUpdated の Id が
			// たまたま一致したときに誤って IsEnabled を復元しかねない。
			_pendingReconnectLocationRestore = null;
		}
		// ローダーが切り替わったら以前のダイヤ情報・サーバー情報は無効。サーバー接続なら
		// 接続時に再要求/再受信され DiagramInfoUpdated / ServerInfoUpdated で改めて
		// 設定される。SetLoader はこの後に NetworkSyncService を接続するため、ここでの
		// クリアが新しい応答を消すことはない。
		CurrentDiagramInfo = null;
		CurrentServerInfo = null;
		if (value is null)
			LoaderSourceLabel = null;

		// WebSocket 以外 (ファイル等) / null に切り替わったら、切断状態と再接続情報は
		// 無意味なのでクリアする。WebSocket → WebSocket の再接続では value も
		// WebSocketNetworkSyncService なので保持される (再接続成功時の false リセットは
		// HandleWebSocketAppLinkAsync 側で行う)。
		if (value is not WebSocketNetworkSyncService)
		{
			IsServerConnectionLost = false;
			IsServerReconnecting = false;
			ClearWebSocketConnectionTracking();
		}

		// Loader 型が変わると ServerConnectionStatus の None 判定が変わる (#266)。
		OnPropertyChanged(nameof(ServerConnectionStatus));
		OnPropertyChanged(nameof(IsTrainSearchAvailable));
	}

	void OnTimetableUpdated(object? sender, TimetableData timetableData)
	{
		logger.Debug("TimetableUpdated: WorkGroupId={0}, WorkId={1}, TrainId={2}, Scope={3}",
			timetableData.WorkGroupId, timetableData.WorkId, timetableData.TrainId, timetableData.Scope);

		// リアルタイム編集対応: 自スコープと一致する更新では選択を維持し、
		// 異なるスコープの更新では現在の表示は無関係なのでそのまま継続する。
		// SelectionManager.Refresh() が各階層で選択 Id を保持しつつ最新データを反映する。
		// - 既存選択が新ペイロードに存在する → 同じ Id の最新インスタンスに差し替え
		// - 既存選択が消えた階層から先 → 先頭にフォールバック
		// この挙動は Scope.All / WorkGroup / Work / Train すべてのケースをカバーする。
		if (Loader is not null)
		{
			logger.Debug("Refreshing selection from Loader cache");
			SelectionManager.Refresh();
		}

		// #311: WS再接続直後の再配信であれば、直前の WorkGroupId/WorkId/TrainId が
		// すべて一致した場合に限り運行状態 (位置情報 ON/OFF) を復元する。
		// 一致しなかった場合は消費するだけで何もしない (フォールバック先の別の
		// 列車に対して運行状態を引き継がない)。
		// SelectionManager.IsAwaitingReconnectData が true の間は、新データがまだ
		// 届いておらず Refresh() が判定を持ち越した状態 (=選択 Id は古いオブジェクトの
		// ままで「一致」に見えるだけ) なので、判定が確定するまで消費せず待つ。
		if (SelectionManager.IsAwaitingReconnectData)
		{
			// まだ判定できない: スナップショットを残したまま次の TimetableUpdated を待つ。
		}
		else if (_pendingReconnectLocationRestore is { } restore)
		{
			_pendingReconnectLocationRestore = null;
			if (ShouldRestoreLocationEnabled(
				restore,
				SelectionManager.SelectedWorkGroup?.Id,
				SelectionManager.SelectedWork?.Id,
				SelectionManager.SelectedTrainData?.Id))
			{
				logger.Info("Reconnect: selection ids matched -> restoring location service IsEnabled");
				InstanceManager.LocationService.IsEnabled = true;
			}
		}
	}

	/// <summary>
	/// #311 の復元条件を切り出した純粋関数: 切断時に記憶した
	/// WorkGroupId/WorkId/TrainId が、再配信後に確定した現在の選択と
	/// すべて一致し、かつ切断前に運行中 (位置情報 ON) だった場合にのみ true。
	/// MAUI/InstanceManager に依存しないため、呼び出し側 (<see cref="OnTimetableUpdated"/>)
	/// が <see cref="TimetableSelectionManager.IsAwaitingReconnectData"/> で
	/// 判定確定後であることを保証した上で呼ぶこと。
	/// </summary>
	internal static bool ShouldRestoreLocationEnabled(
		(string? WorkGroupId, string? WorkId, string? TrainId, bool WasLocationEnabled) snapshot,
		string? currentWorkGroupId,
		string? currentWorkId,
		string? currentTrainId)
	{
		if (!snapshot.WasLocationEnabled)
			return false;

		return
			currentWorkGroupId == snapshot.WorkGroupId &&
			currentWorkId == snapshot.WorkId &&
			currentTrainId == snapshot.TrainId;
	}

	/// <summary>
	/// サーバーから送られた SelectTrain コマンドを反映する。
	/// WorkGroupId / WorkId / TrainId に対応する階層を選択する。
	/// </summary>
	void OnTrainSelectionRequested(object? sender, SelectTrainCommand cmd)
	{
		logger.Info("OnTrainSelectionRequested: WorkGroupId={0}, WorkId={1}, TrainId={2}",
			cmd.WorkGroupId, cmd.WorkId, cmd.TrainId);

		if (cmd.WorkGroupId is not null)
		{
			var wg = SelectionManager.WorkGroupList?.FirstOrDefault(w => w.Id == cmd.WorkGroupId);
			if (wg is not null && SelectionManager.SelectedWorkGroup?.Id != wg.Id)
				SelectionManager.SelectedWorkGroup = wg;
		}

		if (cmd.WorkId is not null)
		{
			var work = SelectionManager.WorkList?.FirstOrDefault(w => w.Id == cmd.WorkId);
			if (work is not null && SelectionManager.SelectedWork?.Id != work.Id)
				SelectionManager.SelectedWork = work;
		}

		if (cmd.TrainId is not null)
		{
			var train = SelectionManager.OrderedTrainDataList?.FirstOrDefault(t => t.Id == cmd.TrainId);
			if (train is not null && SelectionManager.SelectedTrainData?.Id != train.Id)
				SelectionManager.SelectedTrainData = train;
		}
	}

	/// <summary>
	/// サーバーから指示されたヘッダの色 (RGB)。null は端末既定。
	/// View 側はこの値を購読してタイトルバー色を変更する。
	/// </summary>
	[ObservableProperty]
	public partial int? HeaderColorOverride_RGB { get; set; }

	void OnHeaderColorChangeRequested(object? sender, HeaderColorCommand cmd)
	{
		HeaderColorOverride_RGB = cmd.ResetToDefault ? null : cmd.Color_RGB;
	}

	/// <summary>
	/// サーバーから指示されたタイトルバー時刻表示フォーマット ("HH:mm:ss" 等)。
	/// null は端末既定 ("HH:mm:ss" を内部既定とする)。
	/// </summary>
	[ObservableProperty]
	public partial string? HeaderTimeFormat { get; set; }

	void OnTimeFormatChangeRequested(object? sender, TimeFormatCommand cmd)
	{
		HeaderTimeFormat = cmd.Format;
	}

}
