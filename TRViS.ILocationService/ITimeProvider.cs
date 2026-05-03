namespace TRViS.Services;

/// <summary>
/// 時間の進み方の倍率
/// </summary>
public enum TimeProgressionRate
{
	/// <summary>
	/// 1倍速（リアルタイム）
	/// </summary>
	Normal = 1,

	/// <summary>
	/// 30倍速（1秒で30秒分時間が進む。リアルの毎0分が0時0分）
	/// </summary>
	X30 = 30,

	/// <summary>
	/// 60倍速（1秒で60秒分時間が進む。リアルの毎0分と30分が0時0分）
	/// </summary>
	X60 = 60
}

/// <summary>
/// 時刻を提供するインターフェース
/// </summary>
public interface ITimeProvider
{
	/// <summary>
	/// 時間の進み方の倍率
	/// </summary>
	TimeProgressionRate ProgressionRate { get; set; }

	/// <summary>
	/// 現在時刻を取得する（秒単位）
	/// </summary>
	/// <returns>0時0分からの経過秒数</returns>
	int GetCurrentTimeSeconds();

	/// <summary>
	/// 時間の進み方が変更されたときに発生するイベント
	/// </summary>
	event EventHandler<TimeProgressionRate>? ProgressionRateChanged;
}
