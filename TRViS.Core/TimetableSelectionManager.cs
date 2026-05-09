using System.ComponentModel;

using TRViS.IO;
using TRViS.IO.Models;

namespace TRViS.Core;

/// <summary>
/// Single source of truth for timetable selection state.
/// Manages the cascade WorkGroup → Work → TrainData and exposes
/// derived lists. Consumers subscribe to <see cref="PropertyChanged"/>.
/// </summary>
public class TimetableSelectionManager : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private ILoader? _loader;
	public ILoader? Loader
	{
		get => _loader;
		set
		{
			if (ReferenceEquals(_loader, value))
				return;
			_loader = value;
			OnLoaderChanged(value);
		}
	}

	private IReadOnlyList<WorkGroup>? _workGroupList;
	public IReadOnlyList<WorkGroup>? WorkGroupList
	{
		get => _workGroupList;
		private set
		{
			_workGroupList = value;
			RaisePropertyChanged(nameof(WorkGroupList));
		}
	}

	private IReadOnlyList<Work>? _workList;
	public IReadOnlyList<Work>? WorkList
	{
		get => _workList;
		private set
		{
			_workList = value;
			RaisePropertyChanged(nameof(WorkList));
		}
	}

	private IReadOnlyList<TrainData>? _orderedTrainDataList;
	public IReadOnlyList<TrainData>? OrderedTrainDataList
	{
		get => _orderedTrainDataList;
		private set
		{
			_orderedTrainDataList = value;
			RaisePropertyChanged(nameof(OrderedTrainDataList));
		}
	}

	private WorkGroup? _selectedWorkGroup;
	public WorkGroup? SelectedWorkGroup
	{
		get => _selectedWorkGroup;
		set
		{
			if (Equals(_selectedWorkGroup, value))
				return;
			_selectedWorkGroup = value;
			RaisePropertyChanged(nameof(SelectedWorkGroup));
			OnWorkGroupChanged(value);
		}
	}

	private Work? _selectedWork;
	public Work? SelectedWork
	{
		get => _selectedWork;
		set
		{
			if (Equals(_selectedWork, value))
				return;
			_selectedWork = value;
			RaisePropertyChanged(nameof(SelectedWork));
			OnWorkChanged(value);
		}
	}

	private TrainData? _selectedTrainData;
	public TrainData? SelectedTrainData
	{
		get => _selectedTrainData;
		set
		{
			if (Equals(_selectedTrainData, value))
				return;
			_selectedTrainData = value;
			RaisePropertyChanged(nameof(SelectedTrainData));
		}
	}

	// ---------- Cascade ----------

	private void OnLoaderChanged(ILoader? loader)
	{
		// Selection is intentionally cleared rather than auto-picking the first
		// WorkGroup. The Home page presents a tentative-selection picker; the
		// committed selection on this manager only changes when the user presses
		// "Open" (StartHomePage) or via Refresh() (websocket flows).
		_selectedWorkGroup = null;
		_selectedWork = null;
		_selectedTrainData = null;
		WorkList = null;
		OrderedTrainDataList = null;
		RaisePropertyChanged(nameof(SelectedWorkGroup));
		RaisePropertyChanged(nameof(SelectedWork));
		RaisePropertyChanged(nameof(SelectedTrainData));
		WorkGroupList = loader?.GetWorkGroupList();
		// (intentional: no SelectedWorkGroup auto-pick — see comment above)
	}

	/// <summary>
	/// SelectedWorkGroup が null のときに、配下の WorkList / SelectedWork /
	/// OrderedTrainDataList / SelectedTrainData を明示的に空にする。
	/// 「親が空ならば子も必ず空」の不変条件を保つ。
	/// </summary>
	private void ClearChildSelectionsBelowWorkGroup()
	{
		WorkList = null;
		if (_selectedWork is not null)
		{
			_selectedWork = null;
			RaisePropertyChanged(nameof(SelectedWork));
		}
		OrderedTrainDataList = null;
		if (_selectedTrainData is not null)
		{
			_selectedTrainData = null;
			RaisePropertyChanged(nameof(SelectedTrainData));
		}
	}

	/// <summary>
	/// SelectedWork が null のときに、配下の OrderedTrainDataList / SelectedTrainData を明示的に空にする。
	/// </summary>
	private void ClearChildSelectionsBelowWork()
	{
		OrderedTrainDataList = null;
		if (_selectedTrainData is not null)
		{
			_selectedTrainData = null;
			RaisePropertyChanged(nameof(SelectedTrainData));
		}
	}

	private void OnWorkGroupChanged(WorkGroup? workGroup)
	{
		WorkList = null;
		_selectedWork = null;
		RaisePropertyChanged(nameof(SelectedWork));
		OrderedTrainDataList = null;
		_selectedTrainData = null;
		RaisePropertyChanged(nameof(SelectedTrainData));

		if (workGroup is not null && _loader is not null)
		{
			WorkList = _loader.GetWorkList(workGroup.Id);
			SelectedWork = WorkList?.FirstOrDefault();
		}
	}

	private void OnWorkChanged(Work? work)
	{
		// 通常のWork選択切り替え時: 先頭の列車を自動選択する
		RefreshTrainDataForWork(work, preserveSelection: false);
	}

	/// <summary>
	/// 指定の Work に紐づく <see cref="OrderedTrainDataList"/> を再構築する。
	/// </summary>
	/// <param name="work">対象のWork。null の場合はリスト/選択を null にする。</param>
	/// <param name="preserveSelection">
	/// true の場合、現在の <see cref="SelectedTrainData"/> の Id が新リスト内に存在すれば
	/// そのまま選択を維持する (リアルタイム更新時の挙動)。
	/// false の場合、常に先頭の列車を選択する。
	/// </param>
	private void RefreshTrainDataForWork(Work? work, bool preserveSelection)
	{
		if (work is null || _loader is null)
		{
			OrderedTrainDataList = null;
			SelectedTrainData = null;
			return;
		}

		var trainDataList = _loader.GetTrainDataList(work.Id);
		var orderedList = BuildOrderedTrainDataList(trainDataList, _loader);
		OrderedTrainDataList = orderedList;

		if (preserveSelection)
		{
			// 既存の選択を Id で引き直し、まだ存在すれば最新インスタンスに更新する
			string? previousId = _selectedTrainData?.Id;
			if (previousId is not null)
			{
				var matched = orderedList.FirstOrDefault(t => t.Id == previousId);
				if (matched is not null)
				{
					SelectedTrainData = matched;
					return;
				}
			}
		}

		SelectedTrainData = orderedList.Count > 0 ? orderedList[0] : null;
	}

	// ---------- Refresh / Reset ----------

	/// <summary>
	/// Re-reads lists from the current Loader, preserving valid current selections.
	/// Falls back to the first item when the current selection is no longer present.
	/// </summary>
	/// <remarks>
	/// リアルタイム編集対応: 各階層で現在の選択 Id がまだ存在すれば保持し、
	/// その階層のオブジェクトは最新インスタンスに差し替える。
	/// 選択を保持する場合は public setter を経由しない (cascade を避けるため)。
	/// </remarks>
	public void Refresh()
	{
		if (_loader is null)
			return;

		var newWorkGroupList = _loader.GetWorkGroupList();
		WorkGroupList = newWorkGroupList;

		// No prior commit: keep selection null. The Home picker is the source of
		// truth for tentative state; we must not yank the user into a forced commit
		// just because new list data arrived (would make the "no default selection"
		// rule inconsistent across loader types — websocket pushes would still pick
		// a default).
		if (_selectedWorkGroup is null)
			return;

		string? prevWorkGroupId = _selectedWorkGroup.Id;
		var matchedWorkGroup = newWorkGroupList.FirstOrDefault(wg => wg.Id == prevWorkGroupId);

		if (matchedWorkGroup is null)
		{
			// 既存選択が消えた → 先頭にフォールバック (cascade で配下も再構築される)
			var fallback = newWorkGroupList.FirstOrDefault();
			SelectedWorkGroup = fallback;
			// WorkGroup が空ならば、setter の早期 return で配下が初期化されないため明示的に空にする
			if (fallback is null)
				ClearChildSelectionsBelowWorkGroup();
			return;
		}

		// WorkGroup を維持: setter を経由するとカスケードして配下が初期化されるため、
		// フィールドに直接代入して PropertyChanged だけを発火させる。
		if (!Equals(_selectedWorkGroup, matchedWorkGroup))
		{
			_selectedWorkGroup = matchedWorkGroup;
			RaisePropertyChanged(nameof(SelectedWorkGroup));
		}

		var newWorkList = _loader.GetWorkList(matchedWorkGroup.Id);
		WorkList = newWorkList;

		// Same reasoning: don't force a Work commit when none existed.
		if (_selectedWork is null)
			return;

		string? prevWorkId = _selectedWork.Id;
		var matchedWork = newWorkList.FirstOrDefault(w => w.Id == prevWorkId);

		if (matchedWork is null)
		{
			var fallback = newWorkList.FirstOrDefault();
			SelectedWork = fallback;
			// Work が空ならば、配下の Train も明示的に空にする
			if (fallback is null)
				ClearChildSelectionsBelowWork();
			return;
		}

		// Work を維持: 同様にフィールド直接代入で cascade を避ける。
		if (!Equals(_selectedWork, matchedWork))
		{
			_selectedWork = matchedWork;
			RaisePropertyChanged(nameof(SelectedWork));
		}

		// Train リストは再構築しつつ、Id が一致すれば選択を保持する。
		RefreshTrainDataForWork(matchedWork, preserveSelection: true);
	}

	// ---------- Helpers ----------

	private void RaisePropertyChanged(string propertyName)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	/// <summary>
	/// Orders trains starting from chain heads, following NextTrainId chains.
	/// </summary>
	private static List<TrainData> BuildOrderedTrainDataList(IReadOnlyList<TrainData> trainDataList, ILoader loader)
	{
		List<TrainData> orderedList = [];
		HashSet<string> visitedIds = [];
		Dictionary<string, TrainData> byId = [];

		foreach (var td in trainDataList)
		{
			try
			{
				var full = loader.GetTrainData(td.Id);
				if (full is not null)
					byId[td.Id] = full;
			}
			catch
			{
				byId[td.Id] = td;
			}
		}

		HashSet<string> chainHeadIds = [.. byId.Keys];
		foreach (var td in byId.Values)
		{
			if (!string.IsNullOrEmpty(td.NextTrainId))
				chainHeadIds.Remove(td.NextTrainId);
		}

		if (chainHeadIds.Count == 0)
			chainHeadIds = [.. byId.Keys];

		List<List<TrainData>> chainGroups = [];
		foreach (var headId in chainHeadIds)
		{
			List<TrainData> chain = [];
			string? currentId = headId;
			while (!string.IsNullOrEmpty(currentId) && !visitedIds.Contains(currentId))
			{
				if (byId.TryGetValue(currentId, out var td))
				{
					chain.Add(td);
					visitedIds.Add(currentId);
					currentId = td.NextTrainId;
				}
				else
				{
					break;
				}
			}
			if (chain.Count > 0)
				chainGroups.Add(chain);
		}

		chainGroups = [.. chainGroups
			.OrderBy(g => g[0].DayCount)
			.ThenBy(g => GetFirstDepartureTime(g[0]))];

		foreach (var group in chainGroups)
			orderedList.AddRange(group);

		return orderedList;
	}

	private static TimeOnly? GetFirstDepartureTime(TrainData trainData)
	{
		if (trainData.Rows is null)
			return null;
		foreach (var row in trainData.Rows)
		{
			if (row.DepartureTime is not null)
				return row.DepartureTime.ToTimeOnly();
		}
		return null;
	}
}
