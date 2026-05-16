using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.Core;
using TRViS.IO;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.ViewModels;

public partial class AppViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public TimetableSelectionManager SelectionManager { get; } = new();

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

	/// <summary>
	/// サーバーから受信した最新のダイヤ情報 (ダイヤ名・説明など)。未受信／非接続時は null。
	/// 接続情報画面で読み取り専用表示するために購読される。
	/// </summary>
	[ObservableProperty]
	public partial DiagramInfo? CurrentDiagramInfo { get; set; }

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

	internal void SubscribeToLocationService(TRViS.Services.LocationService locationService)
	{
		locationService.TimetableUpdated += OnTimetableUpdated;
		locationService.TrainSelectionRequested += OnTrainSelectionRequested;
		locationService.HeaderColorChangeRequested += OnHeaderColorChangeRequested;
		locationService.TimeFormatChangeRequested += OnTimeFormatChangeRequested;
		locationService.DiagramInfoUpdated += OnDiagramInfoUpdated;
		// NotificationReceived / OperationCommandReceived / ServerInfo は
		// LocationService 側で受信される。OperationCommand の動作 (位置情報 ON/OFF) は
		// LocationService が直接適用する。Notification / ServerInfo の UI 表示は
		// 個別画面側で必要に応じて購読する。
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

	partial void OnLoaderChanged(ILoader? value)
	{
		SelectionManager.Loader = value;
		// ローダーが切り替わったら以前のダイヤ情報は無効。サーバー接続なら
		// 接続時に再要求され DiagramInfoUpdated で改めて設定される。SetLoader は
		// この後に NetworkSyncService を接続するため、ここでのクリアが新しい応答を
		// 消すことはない。
		CurrentDiagramInfo = null;
		if (value is null)
			LoaderSourceLabel = null;

		// WebSocket 以外 (ファイル等) / null に切り替わったら、切断状態と再接続情報は
		// 無意味なのでクリアする。WebSocket → WebSocket の再接続では value も
		// WebSocketNetworkSyncService なので保持される (再接続成功時の false リセットは
		// HandleWebSocketAppLinkAsync 側で行う)。
		if (value is not WebSocketNetworkSyncService)
		{
			IsServerConnectionLost = false;
			ClearWebSocketConnectionTracking();
		}
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
