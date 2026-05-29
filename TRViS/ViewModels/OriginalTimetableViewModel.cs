// 独自時刻表 V1/V2/V4/V6 共有ViewModel — prototype/state.jsx の reducer slices を移植
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO.Models;

namespace TRViS.ViewModels;

public enum Density { Compact, Comfortable, Spacious }
public enum TimetableTheme { Light, Dark }
public enum IconStyle { Emoji, Svg }
public enum HitTarget { Current, Hig }
public enum MarkerKind { None, Flag, Caution, Star }

public partial class OriginalTimetableViewModel : ObservableObject
{
	readonly Dictionary<string, MarkerKind> _markers = new();
	readonly Dictionary<string, string> _memos = new();
	readonly Dictionary<string, bool> _noteOpen = new();
	readonly Dictionary<string, int> _curIdxOverride = new();

	// Density is no longer a user choice — pages assign it from their current
	// width (see each page's ApplyLayoutForWidth) so it expresses the device
	// size tier (Compact / Comfortable / Spacious) that drives fixed row metrics.
	[ObservableProperty]
	public partial Density Density { get; set; } = Density.Comfortable;

	[ObservableProperty]
	public partial TimetableTheme Theme { get; set; } = TimetableTheme.Light;

	[ObservableProperty]
	public partial bool Follow { get; set; } = true;

	[ObservableProperty]
	public partial IconStyle IconStyle { get; set; } = IconStyle.Emoji;

	[ObservableProperty]
	public partial HitTarget HitTarget { get; set; } = HitTarget.Current;

	[ObservableProperty]
	public partial int MarkersVersion { get; set; }

	[ObservableProperty]
	public partial int MemosVersion { get; set; }

	[ObservableProperty]
	public partial int NoteOpenVersion { get; set; }

	[ObservableProperty]
	public partial int CurIdxVersion { get; set; }

	public OriginalTimetableViewModel()
	{
		InstanceManager.AppViewModel.PropertyChanged += OnAppViewModelPropertyChanged;
	}

	void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppViewModel.SelectedTrainData)
			|| e.PropertyName == nameof(AppViewModel.OrderedTrainDataList))
		{
			OnPropertyChanged(nameof(ActiveTrain));
			OnPropertyChanged(nameof(ActiveTrainIdx));
		}
	}

	public TrainData? ActiveTrain => InstanceManager.AppViewModel.SelectedTrainData;

	public int ActiveTrainIdx
	{
		get
		{
			var list = InstanceManager.AppViewModel.OrderedTrainDataList;
			var selected = InstanceManager.AppViewModel.SelectedTrainData;
			if (list is null || selected is null)
				return 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (ReferenceEquals(list[i], selected) || list[i]?.Id == selected.Id)
					return i;
			}
			return 0;
		}
	}

	public void SetTrainIndex(int i)
	{
		var list = InstanceManager.AppViewModel.OrderedTrainDataList;
		if (list is null || list.Count == 0)
			return;
		int clamped = Math.Clamp(i, 0, list.Count - 1);
		InstanceManager.AppViewModel.SelectedTrainData = list[clamped];
	}

	public void NextTrain() => SetTrainIndex(ActiveTrainIdx + 1);
	public void PrevTrain() => SetTrainIndex(ActiveTrainIdx - 1);

	static string Key(string trainId, string rowId) => $"{trainId}:{rowId}";

	public MarkerKind GetMarker(string trainId, string rowId)
		=> _markers.TryGetValue(Key(trainId, rowId), out var v) ? v : MarkerKind.None;

	public void CycleMarker(string trainId, string rowId)
	{
		var current = GetMarker(trainId, rowId);
		var next = current switch
		{
			MarkerKind.None => MarkerKind.Flag,
			MarkerKind.Flag => MarkerKind.Caution,
			MarkerKind.Caution => MarkerKind.Star,
			_ => MarkerKind.None,
		};
		SetMarker(trainId, rowId, next);
	}

	public void SetMarker(string trainId, string rowId, MarkerKind kind)
	{
		var key = Key(trainId, rowId);
		if (kind == MarkerKind.None)
			_markers.Remove(key);
		else
			_markers[key] = kind;
		MarkersVersion++;
	}

	public void ClearMarker(string trainId, string rowId)
		=> SetMarker(trainId, rowId, MarkerKind.None);

	public string GetMemo(string trainId, string rowId)
		=> _memos.TryGetValue(Key(trainId, rowId), out var v) ? v : string.Empty;

	public void SetMemo(string trainId, string rowId, string? text)
	{
		var key = Key(trainId, rowId);
		if (string.IsNullOrWhiteSpace(text))
			_memos.Remove(key);
		else
			_memos[key] = text;
		MemosVersion++;
	}

	public bool IsNoteOpen(string trainId, string rowId)
		=> _noteOpen.TryGetValue(Key(trainId, rowId), out var v) && v;

	public void ToggleNote(string trainId, string rowId)
	{
		var key = Key(trainId, rowId);
		if (_noteOpen.TryGetValue(key, out var v) && v)
			_noteOpen.Remove(key);
		else
			_noteOpen[key] = true;
		NoteOpenVersion++;
	}

	public int? GetCurIdxOverride(string trainId)
		=> _curIdxOverride.TryGetValue(trainId, out var v) ? v : null;

	public void SetCurIdx(string trainId, int idx)
	{
		_curIdxOverride[trainId] = Math.Max(0, idx);
		CurIdxVersion++;
	}

	public void Advance(string trainId, int maxIndex)
	{
		var current = GetCurIdxOverride(trainId) ?? 0;
		_curIdxOverride[trainId] = Math.Clamp(current + 1, 0, Math.Max(0, maxIndex));
		CurIdxVersion++;
	}

	public void Rewind(string trainId)
	{
		var current = GetCurIdxOverride(trainId) ?? 0;
		_curIdxOverride[trainId] = Math.Max(0, current - 1);
		CurIdxVersion++;
	}

	public void Reset()
	{
		_markers.Clear();
		_memos.Clear();
		_noteOpen.Clear();
		_curIdxOverride.Clear();
		Density = Density.Comfortable;
		Theme = TimetableTheme.Light;
		Follow = true;
		IconStyle = IconStyle.Emoji;
		HitTarget = HitTarget.Current;
		MarkersVersion++;
		MemosVersion++;
		NoteOpenVersion++;
		CurIdxVersion++;
	}
}
