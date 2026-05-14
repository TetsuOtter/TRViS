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
	/// Updates timetable view with train data - extracts only display-necessary information.
	///
	/// 同じ列車 (TrainId 一致 + 行数一致) の更新では、既存の行モデルに対して field 単位で
	/// 上書きする (= ObservableCollection は触らない)。これにより:
	///   - 行 UI の dispose / 再生成が発生しない (1 行の DriveTime を直しただけで全行が
	///     消えて上から再描画される、というユーザー視点の不具合が解消される)
	///   - スクロール位置・走行フラグ・マーカー被り行の DriveTime 反転色などの View 状態が維持される
	///   - 値が変わっていない field は ObservableObject の同値ガードで PropertyChanged が
	///     飛ばないので、本当に変わった行 / 本当に変わった field の UI だけが更新される
	/// 列車自体が変わった or 行数が変わった場合のみ ObservableCollection を作り直して
	/// 従来通り全面再構築する。
	/// </summary>
	public void SetTrainData(TrainData? trainData)
	{
		string? newId = trainData?.Id;
		int newCount = trainData?.Rows?.Length ?? 0;

		if (TimetableRebuildPolicy.CanUpdateInPlace(_lastTrainId, CurrentRows.Count, newId, newCount)
			&& trainData?.Rows is not null)
		{
			for (int i = 0; i < trainData.Rows.Length; i++)
				ApplyRowToExistingModel(CurrentRows[i], i, trainData.Rows[i]);

			AfterRemarksText = trainData.AfterRemarks;
			AfterArriveText = trainData.AfterArrive;
			NextTrainId = trainData.NextTrainId;
			// CurrentRows 自体は差し替えていないので、IsRunStarted リセットも
			// marker の再配信も行わない (どちらも UI を巻き戻す方向の副作用なので)。
			return;
		}

		// 全面再構築パス。列車切替 / 初回ロード / 行数変化 / 列車解除 のいずれかに該当。
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

	private VerticalTimetableRowModel BuildRowModel(int index, TimetableRow row)
		=> new()
		{
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
	/// 管理されているのでここでは触らない。
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
