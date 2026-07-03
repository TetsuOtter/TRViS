namespace TRViS.NetworkSyncService;

/// <summary>
/// 列番検索 (<c>SearchTrain</c>) の候補 1 件を表すサマリ。
/// 確認ダイアログの表示 (列番・行路名・始発/終着駅・時刻) と、
/// 確定時の時刻表取得 (<c>RequestTrainTimetable</c>) に必要な ID を保持する。
/// 完全な時刻表 (<c>TrainData</c>) は含まず、確定時に別途取得する (2 段階フロー)。
/// </summary>
/// <param name="WorkGroupId">所属 WorkGroup の ID。</param>
/// <param name="WorkId">所属 Work (行路) の ID。</param>
/// <param name="TrainId">列車の ID。時刻表取得・表示のキーとなる。</param>
/// <param name="TrainNumber">列番。</param>
/// <param name="WorkName">行路名。</param>
/// <param name="Direction">運転方向。-1 = Inbound / 1 = Outbound (未指定は null)。</param>
/// <param name="StartStationName">始発駅名。</param>
/// <param name="StartTime">始発時刻の表示文字列 (例 "09:00")。</param>
/// <param name="EndStationName">終着駅名。</param>
/// <param name="EndTime">終着時刻の表示文字列 (例 "12:30")。</param>
public sealed record TrainSearchResult(
	string? WorkGroupId,
	string? WorkId,
	string? TrainId,
	string? TrainNumber,
	string? WorkName,
	int? Direction,
	string? StartStationName,
	string? StartTime,
	string? EndStationName,
	string? EndTime
);
