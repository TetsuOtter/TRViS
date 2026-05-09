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
		// "Open" (StartHomePage) or via Refresh()/ResetToFirst() (websocket flows).
		_selectedWorkGroup = null;
		_selectedWork = null;
		_selectedTrainData = null;
		WorkList = null;
		OrderedTrainDataList = null;
		RaisePropertyChanged(nameof(SelectedWorkGroup));
		RaisePropertyChanged(nameof(SelectedWork));
		RaisePropertyChanged(nameof(SelectedTrainData));
		WorkGroupList = loader?.GetWorkGroupList();
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
		if (work is not null && _loader is not null)
		{
			var trainDataList = _loader.GetTrainDataList(work.Id);
			var orderedList = BuildOrderedTrainDataList(trainDataList, _loader);
			OrderedTrainDataList = orderedList;
			SelectedTrainData = orderedList.Count > 0 ? orderedList[0] : null;
		}
		else
		{
			OrderedTrainDataList = null;
			SelectedTrainData = null;
		}
	}

	// ---------- Refresh / Reset ----------

	/// <summary>
	/// Re-reads lists from the current Loader, preserving valid current selections.
	/// Falls back to the first item when the current selection is no longer present.
	/// </summary>
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

		bool workGroupStillValid = newWorkGroupList.Any(wg => wg.Id == _selectedWorkGroup.Id);

		if (!workGroupStillValid)
		{
			SelectedWorkGroup = newWorkGroupList.FirstOrDefault();
			return;
		}

		var newWorkList = _loader.GetWorkList(_selectedWorkGroup!.Id);
		WorkList = newWorkList;

		// Same reasoning: don't force a Work commit when none existed.
		if (_selectedWork is null)
			return;

		bool workStillValid = newWorkList.Any(w => w.Id == _selectedWork.Id);

		if (!workStillValid)
		{
			SelectedWork = newWorkList.FirstOrDefault();
			return;
		}

		// Work is still valid — refresh TrainData list
		OnWorkChanged(_selectedWork);
	}

	/// <summary>
	/// Resets selection to the first WorkGroup, but only if a prior commit exists.
	/// Called from <c>AppViewModel.OnTimetableUpdated</c> when a scope-invalidating
	/// timetable change arrives. With no prior commit there is nothing to reset to —
	/// leave the selection null so the Home picker doesn't get a default forced on it.
	/// </summary>
	public void ResetToFirst()
	{
		if (_selectedWorkGroup is null)
			return;
		SelectedWorkGroup = WorkGroupList?.FirstOrDefault();
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
