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
	/// 同じ列車 (= TrainId 一致) の更新では、RowId ベースでモデルインスタンスを再利用しながら
	/// 差分を反映する (ApplySmartDiff):
	///   - RowId が一致する行はインスタンスを再利用し、変更フィールドのみ PropertyChanged が
	///     発火する (= 本当に変わったセルの UI だけが再描画される)。
	///   - 位置だけが変わった行は RowIndex のみ更新 (= Grid.SetRow の呼び出しだけ)。行 UI の
	///     dispose/再生成は一切行われない。
	///   - 新規行は CollectionChanged.Add → View が行 UI を 1 つ追加する。
	///   - 消えた行は CollectionChanged.Remove → View が対応する行 UI を破棄する。
	///   - 並び替えは CollectionChanged.Move → View は RowViewList のエントリを組み替えるだけ。
	/// これにより、行 UI 全体の dispose / 再生成 (= 上から再描画される flash) と、
	/// 走行中フラグ / スクロール位置 / マーカー被り行ハイライトの View 状態リセットを回避する。
	///
	/// 列車自体が違う (= 異なる TrainId) / 初回ロード / null クリアの場合は
	/// ObservableCollection を作り直す全面再構築になる。
	/// </summary>
	public void SetTrainData(TrainData? trainData)
	{
		string? newId = trainData?.Id;

		if (trainData is not null
			&& TimetableRebuildPolicy.IsSameTrainEdit(_lastTrainId, newId))
		{
			TimetableRow[] newRows = trainData.Rows ?? [];
			ApplySmartDiff(newRows);
			AfterRemarksText = trainData.AfterRemarks;
			AfterArriveText = trainData.AfterArrive;
			NextTrainId = trainData.NextTrainId;
			// 新規に Add された行は IsLocationMarkerOnThisRow が既定 false で生まれているので、
			// 保持中のマーカー位置をもう一度全行に分配する。同 index 同値ならば内部で no-op。
			_markerCoordinator.SetRows(CurrentRows);
			return;
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
	/// RowId ベースで既存モデルのインスタンスを再利用しながら差分を反映する。
	///
	/// - RowId が一致する既存モデルはインスタンスを再利用し、位置変更は RowIndex 更新
	///   (→ Grid.SetRow のみ) で対応する。フィールドが同じなら PropertyChanged も抑制される。
	/// - RowId が新しい行はモデルを新規生成 (CollectionChanged.Add → View が行 UI を追加)。
	/// - RowId が消えた行はモデルを削除 (CollectionChanged.Remove → View が行 UI を破棄)。
	/// - 位置変更がある場合は ObservableCollection.Move を発火し、View 側は RowViewList の
	///   エントリだけを組み替える (行 UI の dispose/再生成なし)。
	/// </summary>
	private void ApplySmartDiff(TimetableRow[] newRows)
	{
		// Step 1: 既存モデルの RowId → インデックス マップを構築する。
		var oldIndexById = new Dictionary<string, int>(CurrentRows.Count, StringComparer.Ordinal);
		for (int i = 0; i < CurrentRows.Count; i++)
		{
			string? rid = CurrentRows[i].RowId;
			if (rid is not null)
				oldIndexById[rid] = i;
		}

		// Step 2: 新しい各行について、再利用できる既存モデルを決定する。
		var finalModels = new List<(VerticalTimetableRowModel model, bool isNew)>(newRows.Length);
		var usedOldIndices = new HashSet<int>(newRows.Length);
		for (int i = 0; i < newRows.Length; i++)
		{
			if (oldIndexById.TryGetValue(newRows[i].Id, out int oldIdx))
			{
				finalModels.Add((CurrentRows[oldIdx], false));
				usedOldIndices.Add(oldIdx);
			}
			else
			{
				finalModels.Add((BuildRowModel(i, newRows[i]), true));
			}
		}

		// Step 3: 新しい行リストに存在しない古い行を削除する (後ろから削除して index を安定させる)。
		for (int i = CurrentRows.Count - 1; i >= 0; i--)
		{
			if (!usedOldIndices.Contains(i))
				CurrentRows.RemoveAt(i);
		}
		// 削除後は CurrentRows = [生き残った既存モデル (旧い相対順序を保持)]。

		// Step 4: 新しい順序になるよう Move / Insert を発火しながら CurrentRows を組み替える。
		// 左から処理する。処理済み位置 0..i-1 には正しいモデルが収まっているため、
		// 対象モデルは常に CurrentRows[i..] の中に存在する (= currentPos >= i が成立する)。
		for (int i = 0; i < finalModels.Count; i++)
		{
			var (model, isNew) = finalModels[i];
			if (isNew)
			{
				// 新規モデルは正しい位置に挿入する (CollectionChanged.Add → View が行 UI を追加)。
				if (i >= CurrentRows.Count)
					CurrentRows.Add(model);
				else
					CurrentRows.Insert(i, model);
			}
			else
			{
				// 既存モデルが既に正しい位置にあれば何もしない。
				// 異なる位置にいれば Move する (CollectionChanged.Move → View は RowViewList を並び替える)。
				int currentPos = -1;
				for (int j = i; j < CurrentRows.Count; j++)
				{
					if (ReferenceEquals(CurrentRows[j], model))
					{
						currentPos = j;
						break;
					}
				}
				if (currentPos > i)
					CurrentRows.Move(currentPos, i);
				else if (currentPos < 0)
					logger.Warn("ApplySmartDiff: model at new index {0} (RowId={1}) not found in CurrentRows; likely a duplicate RowId", i, model.RowId);
			}
		}

		// Step 5: フィールドを更新する (RowIndex を含む)。
		// 変更がなければ ObservableObject の同値ガードが PropertyChanged を抑制するため、
		// 「位置のみ移動」な行の再描画は Grid.SetRow の呼び出しだけになる。
		// CurrentRows.Count との Min は、重複 RowId など不正データで Step 4 が currentPos=-1 の
		// ままになった行が存在する場合の IndexOutOfRangeException を防ぐ防御的ガード。
		int updateCount = Math.Min(newRows.Length, CurrentRows.Count);
		for (int i = 0; i < updateCount; i++)
			ApplyRowToExistingModel(CurrentRows[i], i, newRows[i]);
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
	/// 管理されているのでここでは触らない。<c>RowId</c> は ApplySmartDiff が RowId で
	/// 既存モデルを引いているため、model.RowId == row.Id が保証されており上書き不要。
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
