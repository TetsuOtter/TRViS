namespace TRViS.Services;

/// <summary>
/// 時刻を提供する実装クラス
/// </summary>
public class AppTimeProvider : ITimeProvider
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private TimeProgressionRate _progressionRate = TimeProgressionRate.Normal;

	/// <summary>
	/// 時間の進み方の倍率
	/// </summary>
	public TimeProgressionRate ProgressionRate
	{
		get => _progressionRate;
		set
		{
			if (_progressionRate == value)
				return;

			logger.Info("TimeProgressionRate changed from {0} to {1}", _progressionRate, value);
			_progressionRate = value;
			ProgressionRateChanged?.Invoke(this, value);
		}
	}

	/// <summary>
	/// 時間の進み方が変更されたときに発生するイベント
	/// </summary>
	public event EventHandler<TimeProgressionRate>? ProgressionRateChanged;

	/// <summary>
	/// 現在時刻を取得する（秒単位）
	/// </summary>
	/// <returns>0時0分からの経過秒数</returns>
	public int GetCurrentTimeSeconds()
	{
		DateTime now = DateTime.Now;
		
		switch (ProgressionRate)
		{
			case TimeProgressionRate.Normal:
				// 通常速度：現在時刻をそのまま返す
				return (int)now.TimeOfDay.TotalSeconds;

			case TimeProgressionRate.X30:
				// 30倍速：リアルの毎0分が0時0分になる
				// 1秒で30秒分時間が進む = 2分で1時間分進む
				// リアルタイムの分の部分を基準にする
				{
					int realMinutes = now.Minute;
					int realSeconds = now.Second;
					// 直前の0分からの経過時間（秒）
					int elapsedSeconds = realMinutes * 60 + realSeconds;
					// 30倍速で進んだ時間
					int virtualSeconds = (elapsedSeconds * 30) % 86400; // 86400秒 = 24時間
					return virtualSeconds;
				}

			case TimeProgressionRate.X60:
				// 60倍速：リアルの毎0分と30分が0時0分になる
				// 1秒で60秒分時間が進む = 1分で1時間分進む
				{
					int realMinutes = now.Minute;
					int realSeconds = now.Second;
					// 直前の0分または30分からの経過時間（秒）
					int minutesFromBase = realMinutes % 30;
					int elapsedSeconds = minutesFromBase * 60 + realSeconds;
					// 60倍速で進んだ時間
					int virtualSeconds = (elapsedSeconds * 60) % 86400; // 86400秒 = 24時間
					return virtualSeconds;
				}

			default:
				logger.Warn("Unknown TimeProgressionRate: {0}, using Normal", ProgressionRate);
				return (int)now.TimeOfDay.TotalSeconds;
		}
	}
}
