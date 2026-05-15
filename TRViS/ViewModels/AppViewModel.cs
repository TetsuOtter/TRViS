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
	/// Raised after a server-driven load (HTTP / WebSocket TRViS.LocalServers
	/// integration) has set the loader and committed a WorkGroup selection, to
	/// request that the UI jump straight to the timetable instead of leaving the
	/// user on the Home picker. StartHomePage subscribes and performs the actual
	/// navigation (it owns navigation + modal lifecycle; raising an event here
	/// avoids doing Shell navigation from the AppLink handler while the
	/// ConnectServerDialog modal may still be on the stack).
	/// </summary>
	public event EventHandler? AutoNavigateToTimetableRequested;

	internal void RequestAutoNavigateToTimetable()
		=> AutoNavigateToTimetableRequested?.Invoke(this, EventArgs.Empty);

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
		// NotificationReceived / OperationCommandReceived / ServerInfo / DiagramInfo は
		// LocationService 側で受信される。OperationCommand の動作 (位置情報 ON/OFF) は
		// LocationService が直接適用する。Notification / ServerInfo / DiagramInfo の UI 表示は
		// 個別画面側で必要に応じて購読する。
	}

	partial void OnLoaderChanged(ILoader? value)
	{
		SelectionManager.Loader = value;
		if (value is null)
			LoaderSourceLabel = null;
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
