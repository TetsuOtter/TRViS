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
	/// Updates timetable view with train data - extracts only display-necessary information
	/// </summary>
	public void SetTrainData(TrainData? trainData)
	{
		// Extract and set display information
		CurrentRows = new ObservableCollection<VerticalTimetableRowModel>(
			(trainData?.Rows ?? []).Select((row, index) => new VerticalTimetableRowModel
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
				MarkerColor = row.DefaultMarkerColor_RGB is not null ? Color.FromRgb((byte)((row.DefaultMarkerColor_RGB >> 16) & 0xFF), (byte)((row.DefaultMarkerColor_RGB >> 8) & 0xFF), (byte)(row.DefaultMarkerColor_RGB & 0xFF)) : null,
				MarkerText = row.DefaultMarkerText,
			})
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
}
