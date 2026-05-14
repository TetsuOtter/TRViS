using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

using TRViS.DTAC.Logic;
using TRViS.IO.Models;
using TRViS.Services;
using NLog;

namespace TRViS.DTAC.ViewModels;

public partial class VerticalTimetableViewModel : ObservableObject
{
	private static readonly Logger logger = LoggerService.GetGeneralLogger();

	public static readonly GridLength RowHeight = new(60);

	// CurrentLocationMarker と重なる行の DriveTime ラベル色 (白文字反転) を、
	// 行モデル列の差し替え (= SetTrainData) と View 側のマーカー位置更新の双方から
	// 一貫して管理する。詳細は LocationMarkerRowCoordinator のコメント参照。
	private readonly LocationMarkerRowCoordinator _markerCoordinator = new();

	// 直近に表示中の Train の Id。SetTrainData が「同じ列車の field 編集」を検知し、
	// ObservableCollection 差し替えではなく in-place 更新に倒すために使う。
	private string? _lastTrainId = null;

	[ObservableProperty]
	public partial ObservableCollection<VerticalTimetableRowModel> CurrentRows { get; set; } = [];

	[ObservableProperty]
	public partial bool IsMarkingMode { get; set; } = false;

	[ObservableProperty]
	public partial bool IsRunStarted { get; set; } = false;

	[ObservableProperty]
	public partial string? AfterRemarksText { get; set; } = null;

	[ObservableProperty]
	public partial string? AfterArriveText { get; set; } = null;

	[ObservableProperty]
	public partial string? NextTrainId { get; set; } = null;

	[ObservableProperty]
	public partial bool IsLocationServiceEnabled { get; set; } = false;

	/// <summary>
	/// CurrentLocationMarker (現在位置マーカー) が指している行 index。
	/// View が ApplyPresenterState から書き込む。
	/// </summary>
	[ObservableProperty]
	public partial int MarkerRowIndex { get; set; } = -1;

	/// <summary>
	/// CurrentLocationMarker を画面に出しているかどうか。
	/// View が ApplyPresenterState から書き込む。
	/// </summary>
	[ObservableProperty]
	public partial bool IsMarkerVisible { get; set; } = false;

	partial void OnIsMarkingModeChanged(bool value)
	{
		foreach (var row in CurrentRows)
		{
			row.IsMarkingMode = value;
		}
	}

	partial void OnMarkerRowIndexChanged(int value)
		=> _markerCoordinator.MarkerRowIndex = value;

	partial void OnIsMarkerVisibleChanged(bool value)
		=> _markerCoordinator.IsMarkerVisible = value;

	/// <summary>
	/// Updates timetable view with train data.
	///
	/// 同じ列車 (= TrainId 一致) の更新では、ObservableCollection 自体を作り直さず、
	/// 既存の行モデル列を mutate する形で差分を反映する:
	///   - 重なっている position (= 既存と新の両方に存在する 0..min(N) -1) は field 上書き。
	///     ObservableObject の同値ガードにより、本当に変わった field の PropertyChanged だけ
	///     飛ぶので、変更のあった行 (例: DriveTimeMM を直した 1 行) の UI だけが再描画される。
	///   - 新規行は末尾に Add (= CollectionChanged.Add)。View 側は incremental に行 UI を 1 つ追加する。
	///   - 余剰行は末尾から Remove (= CollectionChanged.Remove)。View 側は対応する行 UI を 1 つ捨てる。
	/// これにより、行 UI 全体の dispose / 再生成 (= 上から再描画される flash) と、
	/// 走行中フラグ / スクロール位置 / マーカー被り行ハイライトの View 状態リセットを回避する。
	///
	/// ただし「行が中間で挿入・削除された」場合は position が Id でずれるので、上記 mutate を
	/// 適用すると見た目上 行のデータが横滑り表示されてしまう。RowId による position alignment
	/// チェックでこれを検出し、ずれた場合のみ ObservableCollection を作り直す fallback に倒す。
	///
	/// 列車自体が違う (= 異なる TrainId) / 初回ロード / null クリアの場合も
	/// ObservableCollection を作り直す全面再構築になる。
	/// </summary>
	public void SetTrainData(TrainData? trainData)
	{
		string? newId = trainData?.Id;

		if (trainData is not null
			&& TimetableRebuildPolicy.IsSameTrainEdit(_lastTrainId, newId))
		{
			TimetableRow[] newRows = trainData.Rows ?? [];
			if (IsPositionAlignedByRowId(CurrentRows, newRows))
			{
				ApplyPositionAlignedDiff(newRows);
				AfterRemarksText = trainData.AfterRemarks;
				AfterArriveText = trainData.AfterArrive;
				NextTrainId = trainData.NextTrainId;
				// 新規に Add された行は IsLocationMarkerOnThisRow が既定 false で生まれているので、
				// 保持中のマーカー位置をもう一度全行に分配する。同 index 同値ならば内部で no-op。
				_markerCoordinator.SetRows(CurrentRows);
				return;
			}
			// position alignment 不一致 (= 中間挿入/削除) は fall-through で全面再構築。
		}

		// 全面再構築パス: 列車切替 / 初回ロード / null クリア / 中間挿入/削除のいずれか。
		_lastTrainId = newId;
		CurrentRows = new ObservableCollection<VerticalTimetableRowModel>(
			(trainData?.Rows ?? []).Select((row, index) => BuildRowModel(index, row))
		);
		AfterRemarksText = trainData?.AfterRemarks;
		AfterArriveText = trainData?.AfterArrive;
		NextTrainId = trainData?.NextTrainId;

		// 行モデルが差し替わったので、保持中のマーカー位置を新しい行へ反映する。
		// (これを忘れると、マーカーが同じ index にあるのに新しい行が黒文字のまま残る)
		_markerCoordinator.SetRows(CurrentRows);

		// Reset run started state
		IsRunStarted = false;
	}

	/// <summary>
	/// 現在の行列と新しい行配列が「同 index に同じ RowId が並ぶ」関係になっているかを返す。
	/// true なら mutate ベースの差分更新で対応可能 (= 末尾の Add / Remove + field 上書き)。
	/// false (= 中間挿入や中間削除で position が Id ベースでずれた) なら、ObservableCollection を
	/// 作り直す全面再構築に倒す必要がある。
	/// </summary>
	private static bool IsPositionAlignedByRowId(
		IReadOnlyList<VerticalTimetableRowModel> oldRows,
		TimetableRow[] newRows
	)
	{
		int overlap = Math.Min(oldRows.Count, newRows.Length);
		for (int i = 0; i < overlap; i++)
		{
			if (!string.Equals(oldRows[i].RowId, newRows[i].Id, StringComparison.Ordinal))
				return false;
		}
		return true;
	}

	/// <summary>
	/// 同 index 同 RowId の前提のもと、既存行を field 上書き、超過分を末尾 Add、不足分を末尾 Remove する。
	/// ObservableCollection の Add / Remove は CollectionChanged を発火するため、View 側は incremental
	/// に対応する行 UI を追加/削除できる (= 全 dispose の flash を回避できる)。
	/// </summary>
	private void ApplyPositionAlignedDiff(TimetableRow[] newRows)
	{
		int oldCount = CurrentRows.Count;
		int newCount = newRows.Length;
		int overlap = Math.Min(oldCount, newCount);

		// 1. 重なっている position は field 上書き。
		for (int i = 0; i < overlap; i++)
			ApplyRowToExistingModel(CurrentRows[i], i, newRows[i]);

		// 2. 末尾追加 (newCount > oldCount のときだけ実行される)。
		for (int i = oldCount; i < newCount; i++)
			CurrentRows.Add(BuildRowModel(i, newRows[i]));

		// 3. 末尾削除 (oldCount > newCount のときだけ実行される)。逆順で index 安定。
		for (int i = oldCount - 1; i >= newCount; i--)
			CurrentRows.RemoveAt(i);
	}

	private VerticalTimetableRowModel BuildRowModel(int index, TimetableRow row)
		=> new()
		{
			RowId = row.Id,
			RowIndex = index,
			IsInfoRow = row.IsInfoRow,
			InfoText = row.IsInfoRow ? row.StationName : null,
			IsMarkingMode = IsMarkingMode,
			DriveTimeMM = row.DriveTimeMM?.ToString(),
			DriveTimeSS = row.DriveTimeSS?.ToString(),
			StationName = row.StationName,
			IsPass = row.IsPass,
			ArrivalTime = row.ArriveTime,
			HasBracket = row.HasBracket,
			DepartureTime = row.DepartureTime,
			IsLastStop = row.IsLastStop,
			IsOperationOnlyStop = row.IsOperationOnlyStop,
			TrackName = row.TrackName,
			RunInLimit = row.RunInLimit?.ToString(),
			RunOutLimit = row.RunOutLimit?.ToString(),
			Remarks = row.Remarks,
			MarkerColor = ToMarkerColor(row.DefaultMarkerColor_RGB),
			MarkerText = row.DefaultMarkerText,
		};

	/// <summary>
	/// 既存の行モデルに field 単位で上書きする。各 setter は <c>ObservableObject</c> の
	/// 同値ガードで PropertyChanged を抑制するので、変更のあった field の UI だけが更新される。
	/// <c>IsMarkingMode</c> / <c>IsLocationMarkerOnThisRow</c> は別経路 (toggle / coordinator) で
	/// 管理されているのでここでは触らない。<c>RowId</c> は position alignment 判定で使った後の
	/// 同じ row に当てに行っているはずなので明示的に上書きしない (元から同じ Id)。
	/// </summary>
	private static void ApplyRowToExistingModel(VerticalTimetableRowModel model, int index, TimetableRow row)
	{
		model.RowIndex = index;
		model.IsInfoRow = row.IsInfoRow;
		model.InfoText = row.IsInfoRow ? row.StationName : null;
		model.DriveTimeMM = row.DriveTimeMM?.ToString();
		model.DriveTimeSS = row.DriveTimeSS?.ToString();
		model.StationName = row.StationName;
		model.IsPass = row.IsPass;
		model.ArrivalTime = row.ArriveTime;
		model.HasBracket = row.HasBracket;
		model.DepartureTime = row.DepartureTime;
		model.IsLastStop = row.IsLastStop;
		model.IsOperationOnlyStop = row.IsOperationOnlyStop;
		model.TrackName = row.TrackName;
		model.RunInLimit = row.RunInLimit?.ToString();
		model.RunOutLimit = row.RunOutLimit?.ToString();
		model.Remarks = row.Remarks;
		model.MarkerColor = ToMarkerColor(row.DefaultMarkerColor_RGB);
		model.MarkerText = row.DefaultMarkerText;
	}

	private static Color? ToMarkerColor(int? rgb)
		=> rgb is null
			? null
			: Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
}
