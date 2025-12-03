using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;

namespace TRViS.ViewModels;

public partial class AppViewModel : ObservableObject
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	[ObservableProperty]
	ILoader? _Loader;

	[ObservableProperty]
	IReadOnlyList<WorkGroup>? _WorkGroupList;

	[ObservableProperty]
	IReadOnlyList<Work>? _WorkList;

	[ObservableProperty]
	WorkGroup? _SelectedWorkGroup;
	Work? _SelectedWork;
	public Work? SelectedWork
	{
		get => _SelectedWork;
		set
		{
			if (SetProperty(ref _SelectedWork, value))
			{
				OnSelectedWorkChanged(value);
			}
		}
	}

	TrainData? _SelectedTrainData;
	public TrainData? SelectedTrainData
	{
		get => _SelectedTrainData;
		set => SetProperty(ref _SelectedTrainData, value);
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
	double _WindowHeight;

	[ObservableProperty]
	double _WindowWidth;

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

		// LocationService の時刻表更新イベントをサブスクライブ
		InstanceManager.LocationService.TimetableUpdated += OnTimetableUpdated;
	}

	partial void OnLoaderChanged(ILoader? value)
	{
		SelectedWorkGroup = null;
		WorkGroupList = value?.GetWorkGroupList();
		SelectedWorkGroup = WorkGroupList?.FirstOrDefault();
	}

	partial void OnSelectedWorkGroupChanged(WorkGroup? value)
	{
		WorkList = null;
		SelectedWork = null;

		if (value is not null)
		{
			WorkList = Loader?.GetWorkList(value.Id);

			SelectedWork = WorkList?.FirstOrDefault();
		}
	}

	void OnSelectedWorkChanged(Work? value)
	{
		logger.Debug("Work: {0}", value?.Id ?? "null");
		if (value is not null)
		{
			string? trainId = Loader?.GetTrainDataList(value.Id)?.FirstOrDefault()?.Id;
			logger.Debug("FirstTrainId: {0}", trainId ?? "null");
			var selectedTrainData = trainId is null ? null : Loader?.GetTrainData(trainId);
			// バインディングシステムはrecordの値ベース比較を使用する可能性があるため、
			// 一度nullに設定してから新しい値を設定することで、確実に更新を伝播させる
			_SelectedTrainData = null;
			OnPropertyChanged(nameof(SelectedTrainData));
			_SelectedTrainData = selectedTrainData;
			OnPropertyChanged(nameof(SelectedTrainData));
			logger.Debug("SelectedTrainData: {0} ({1})", SelectedTrainData?.Id ?? "null", selectedTrainData?.Id ?? "null");
		}
		else
		{
			SelectedTrainData = null;
		}
	}

	/// <summary>
	/// 現在選択中の TrainData を Loader から再取得して更新します。
	/// 時刻表更新時に、現在選択中の列車の情報を最新化するために使用します。
	/// 選択中の列車が存在しない場合は、最初の列車を選択します。
	/// </summary>
	void RefreshSelectedTrainData()
	{
		if (SelectedWork is null || Loader is null)
		{
			SelectedTrainData = null;
			return;
		}

		string? currentTrainId = SelectedTrainData?.Id;
		IReadOnlyList<TrainData>? trainDataList = Loader.GetTrainDataList(SelectedWork.Id);

		// 現在選択中の列車が存在するか確認
		string? trainIdToSelect = null;
		if (currentTrainId is not null && trainDataList?.Any(t => t.Id == currentTrainId) == true)
		{
			// 現在選択中の列車が存在する場合は、その列車を再選択
			trainIdToSelect = currentTrainId;
			logger.Debug("RefreshSelectedTrainData: Keeping current train selection: {0}", trainIdToSelect);
		}
		else
		{
			// 現在選択中の列車が存在しない場合は、最初の列車を選択
			trainIdToSelect = trainDataList?.FirstOrDefault()?.Id;
			logger.Debug("RefreshSelectedTrainData: Current train not found, selecting first train: {0}", trainIdToSelect ?? "null");
		}

		TrainData? selectedTrainData = trainIdToSelect is null ? null : Loader.GetTrainData(trainIdToSelect);
		
		// バインディングシステムはrecordの値ベース比較を使用する可能性があるため、
		// 一度nullに設定してから新しい値を設定することで、確実に更新を伝播させる
		_SelectedTrainData = null;
		OnPropertyChanged(nameof(SelectedTrainData));
		_SelectedTrainData = selectedTrainData;
		OnPropertyChanged(nameof(SelectedTrainData));
		logger.Debug("RefreshSelectedTrainData: SelectedTrainData updated to: {0}", selectedTrainData?.Id ?? "null");
	}

	void OnTimetableUpdated(object? sender, TimetableData timetableData)
	{
		logger.Debug("TimetableUpdated: WorkGroupId={0}, WorkId={1}, TrainId={2}, Scope={3}",
			timetableData.WorkGroupId, timetableData.WorkId, timetableData.TrainId, timetableData.Scope);

		// 時刻表の変更スコープに応じて、表示継続可能か判定する
		bool canContinue = CanContinueCurrentTimetable(timetableData);

		if (!canContinue)
		{
			// 表示継続不可の場合は初期状態に戻す
			logger.Info("Timetable changed and cannot continue -> reset to initial state");
			ResetToInitialTimetable();
		}

		// Loader のキャッシュが更新された可能性があるため、UI を再読み込み
		// 特に WebSocket の場合は Loader のデータが動的に更新されるため、毎回再読み込みする必要がある
		RefreshLoaderDisplay();
	}

	private void RefreshLoaderDisplay()
	{
		if (Loader is null)
			return;

		logger.Debug("RefreshLoaderDisplay: Refreshing UI from Loader cache");
		WorkGroupList = Loader.GetWorkGroupList();

		// 現在選択中の WorkGroup が存在しない場合は、最初のものを選択
		if (SelectedWorkGroup is not null && !WorkGroupList?.Any(wg => wg.Id == SelectedWorkGroup.Id) == true)
		{
			SelectedWorkGroup = WorkGroupList?.FirstOrDefault();
		}
		else if (SelectedWorkGroup is null && WorkGroupList?.Count > 0)
		{
			SelectedWorkGroup = WorkGroupList.FirstOrDefault();
		}

		// WorkList も更新
		if (SelectedWorkGroup is not null)
		{
			WorkList = Loader.GetWorkList(SelectedWorkGroup.Id);

			// 現在選択中の Work が存在しない場合は、最初のものを選択
			if (SelectedWork is not null && !WorkList?.Any(w => w.Id == SelectedWork.Id) == true)
			{
				SelectedWork = WorkList?.FirstOrDefault();
			}
			else if (SelectedWork is null && WorkList?.Count > 0)
			{
				SelectedWork = WorkList.FirstOrDefault();
			}
			else
			{
				// WorkList が更新された場合、選択中の Work でも UI を更新する必要がある
				// 現在選択中の TrainData を更新（保持）する
				RefreshSelectedTrainData();
			}
		}
	}

	bool CanContinueCurrentTimetable(TimetableData timetableData)
	{
		// 変更スコープに基づいて判定する
		return timetableData.Scope switch
		{
			// All：全体の情報が更新された場合は表示継続不可
			TimetableScopeType.All => false,

			// WorkGroup単位の変更：現在の選択がこのWorkGroupと異なる場合のみ継続可能
			TimetableScopeType.WorkGroup => SelectedWorkGroup?.Id != timetableData.WorkGroupId,

			// Work単位の変更：現在の選択がこのWorkと異なる場合のみ継続可能
			TimetableScopeType.Work => SelectedWork?.Id != timetableData.WorkId,

			// Train単位の変更：現在の選択がこのTrainと異なる場合のみ継続可能
			TimetableScopeType.Train => SelectedTrainData?.Id != timetableData.TrainId,

			_ => true
		};
	}

	void ResetToInitialTimetable()
	{
		// Loader情報をリセットして、表示を初期状態に戻す
		if (Loader is not null)
		{
			var workGroupList = Loader.GetWorkGroupList();
			SelectedWorkGroup = workGroupList?.FirstOrDefault();
		}
	}
}
