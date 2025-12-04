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

	[ObservableProperty]
	IReadOnlyList<TrainData>? _OrderedTrainDataList;

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
		if (value is not null && Loader is not null)
		{
			// Get the list of trains and create an ordered list based on NextTrainId chains
			var trainDataList = Loader.GetTrainDataList(value.Id);
			var orderedTrainList = GetOrderedTrainDataList(trainDataList, Loader);
			OrderedTrainDataList = orderedTrainList;

			// Select the first train in the ordered list
			if (orderedTrainList.Count > 0)
			{
				logger.Debug("FirstTrainId (from ordered list): {0}", orderedTrainList[0].Id);
				SelectedTrainData = orderedTrainList[0];
			}
			else
			{
				SelectedTrainData = null;
			}
			logger.Debug("SelectedTrainData: {0}", SelectedTrainData?.Id ?? "null");
		}
		else
		{
			OrderedTrainDataList = null;
			SelectedTrainData = null;
		}
	}

	/// <summary>
	/// Orders trains starting from chain heads, following NextTrainId chain.
	/// A chain head is a train that is not pointed to by any other train's NextTrainId.
	/// If a visited train is referenced, stops the chain.
	/// Chains are ordered by the DayCount of the head train, then by departure time of the first station.
	/// </summary>
	private List<TrainData> GetOrderedTrainDataList(IReadOnlyList<TrainData> trainDataList, ILoader loader)
	{
		List<TrainData> orderedList = [];
		HashSet<string> visitedTrainIds = [];
		Dictionary<string, TrainData> trainDataById = [];

		// Build a map of train IDs to train data for quick lookup
		// Need to fetch full TrainData objects which include NextTrainId
		foreach (var trainData in trainDataList)
		{
			try
			{
				var fullTrainData = loader.GetTrainData(trainData.Id);
				if (fullTrainData is not null)
				{
					trainDataById[trainData.Id] = fullTrainData;
				}
			}
			catch (Exception ex)
			{
				logger.Warn(ex, "Failed to get full train data for {0}", trainData.Id);
				trainDataById[trainData.Id] = trainData;
			}
		}

		// Find all chain heads (trains that are not pointed to by any other train's NextTrainId)
		HashSet<string> chainHeadIds = [.. trainDataById.Keys];
		foreach (var trainData in trainDataById.Values)
		{
			if (!string.IsNullOrEmpty(trainData.NextTrainId))
			{
				chainHeadIds.Remove(trainData.NextTrainId);
			}
		}

		// If no chain heads found (circular references), treat all trains as chain heads
		if (chainHeadIds.Count == 0)
		{
			chainHeadIds = [.. trainDataById.Keys];
		}

		// Group chains by their head train
		List<List<TrainData>> chainGroups = [];
		foreach (var chainHeadId in chainHeadIds)
		{
			List<TrainData> chain = [];
			string? currentId = chainHeadId;
			while (!string.IsNullOrEmpty(currentId) && !visitedTrainIds.Contains(currentId))
			{
				if (trainDataById.TryGetValue(currentId, out var trainData))
				{
					chain.Add(trainData);
					visitedTrainIds.Add(currentId);
					currentId = trainData.NextTrainId;
				}
				else
				{
					// Invalid NextTrainId reference - stop the chain
					logger.Warn("Next train ID '{0}' not found", currentId);
					break;
				}
			}

			// If chain ended because of a visited train (circular reference), log it
			if (!string.IsNullOrEmpty(currentId) && visitedTrainIds.Contains(currentId))
			{
				logger.Debug("Chain stopped at already-visited train {0}", currentId);
			}

			if (chain.Count > 0)
			{
				chainGroups.Add(chain);
			}
		}

		// Sort chain groups by DayCount of head train, then by departure time of first station
		chainGroups = [.. chainGroups
			.OrderBy(group => group[0].DayCount)
			.ThenBy(group => GetFirstDepartureTime(group[0]))];

		// Flatten the groups into the final ordered list
		foreach (var group in chainGroups)
		{
			orderedList.AddRange(group);
		}

		logger.Debug("Ordered {0} trains (original: {1})", orderedList.Count, trainDataList.Count);
		return orderedList;
	}

	private TimeOnly? GetFirstDepartureTime(TrainData trainData)
	{
		if (trainData.Rows.Length == 0)
			return null;

		// Find the first row with a departure time
		foreach (var row in trainData.Rows)
		{
			if (row.DepartureTime is not null)
			{
				return row.DepartureTime.ToTimeOnly();
			}
		}

		return null;
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
				// OnSelectedWorkChanged を明示的に呼ぶ
				OnSelectedWorkChanged(SelectedWork);
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
