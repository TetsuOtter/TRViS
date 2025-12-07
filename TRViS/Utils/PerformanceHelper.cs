using NLog;

namespace TRViS.Utils;

/// <summary>
/// デバイスのCPU情報に基づいてパフォーマンスレベルを判定し、
/// 適切なUI描画遅延時間を提供するヘルパークラス
/// </summary>
public static class PerformanceHelper
{
	/// <summary>CPU パフォーマンスレベルの列挙型</summary>
	public enum PerformanceLevel
	{
		Low,
		Medium,
		High
	}

	/// <summary>行の追加開始から行の設定を始めるまでの遅延時間（ミリ秒）</summary>
	public static readonly int DelayBeforeSettingRowsMs;

	/// <summary>UIレンダリングのための遅延時間（ミリ秒）</summary>
	public static readonly int RowRenderDelayMs;

	private const int FRAME_DURATION_30FPS_MS = 1000 / 30;
	private const int FRAME_DURATION_60FPS_MS = 1000 / 60;
	private const int FRAME_DURATION_120FPS_MS = 1000 / 120;

	/// <summary>
	/// パフォーマンスレベルに応じた遅延設定（初期遅延時間）を取得します
	/// </summary>
	/// <param name="performanceLevel">パフォーマンスレベル</param>
	/// <returns>初期遅延時間（ミリ秒）</returns>
	private static int GetBeforeRowRenderDelayConfigForLevel(PerformanceLevel performanceLevel)
		=> performanceLevel switch
		{
			PerformanceLevel.Low => 250,
			PerformanceLevel.Medium => 100,
			PerformanceLevel.High => 25,
			_ => 100
		};

	/// <summary>
	/// パフォーマンスレベルに応じたUI描画遅延時間を取得します
	/// </summary>
	/// <param name="performanceLevel">パフォーマンスレベル</param>
	/// <returns>UI描画遅延時間（ミリ秒）</returns>
	private static int GetRowRenderDelayMsForLevel(PerformanceLevel performanceLevel)
		=> performanceLevel switch
		{
			PerformanceLevel.Low => FRAME_DURATION_30FPS_MS,
			PerformanceLevel.Medium => FRAME_DURATION_60FPS_MS,
			PerformanceLevel.High => FRAME_DURATION_120FPS_MS,
			_ => FRAME_DURATION_60FPS_MS
		};

	static PerformanceHelper()
	{
		PerformanceLevel level = GetPerformanceLevelFromDeviceModel();

		DelayBeforeSettingRowsMs = GetBeforeRowRenderDelayConfigForLevel(level);
		RowRenderDelayMs = GetRowRenderDelayMsForLevel(level);
	}

	/// <summary>
	/// デバイスのモデル名からパフォーマンスレベルを直接判定します
	/// </summary>
	/// <returns>パフォーマンスレベル</returns>
	private static PerformanceLevel GetPerformanceLevelFromDeviceModel()
	{
#if IOS
		string deviceModel = DeviceInfo.Current.Model ?? "";

		// iPad
		if (deviceModel.Contains("iPad"))
		{
			// iPad mini 2 (iPad4,5), iPad mini 3 (iPad4,6)
			if (deviceModel.Contains("iPad4,5") || deviceModel.Contains("iPad4,6"))
				return PerformanceLevel.Low;
			// iPad mini 4 (iPad5,1)
			if (deviceModel.Contains("iPad5,1"))
				return PerformanceLevel.Medium;
			return PerformanceLevel.High;
		}

		if (deviceModel.Contains("iPhone"))
		{
			// iPhone 5 (iPhone3,1/3,2), iPhone 5C (iPhone5,3/5,4), iPhone 5S (iPhone6,1/6,2)
			if (deviceModel.Contains("iPhone3,1") || deviceModel.Contains("iPhone3,2") ||
					deviceModel.Contains("iPhone5,3") || deviceModel.Contains("iPhone5,4") ||
					deviceModel.Contains("iPhone6,1") || deviceModel.Contains("iPhone6,2"))
				return PerformanceLevel.Low;
			// iPhone 6 (iPhone7,2), iPhone 6 Plus (iPhone7,1)
			if (deviceModel.Contains("iPhone7,1") || deviceModel.Contains("iPhone7,2"))
				return PerformanceLevel.Medium;
			// iPhone 7 (iPhone9,1/9,3), iPhone 7 Plus (iPhone9,2/9,4)
			if (deviceModel.Contains("iPhone9,1") || deviceModel.Contains("iPhone9,2") ||
					deviceModel.Contains("iPhone9,3") || deviceModel.Contains("iPhone9,4"))
				return PerformanceLevel.Medium;
			// iPhone 8 (iPhone10,1/10,4), iPhone 8 Plus (iPhone10,2/10,5)
			if (deviceModel.Contains("iPhone10,1") || deviceModel.Contains("iPhone10,2") ||
					deviceModel.Contains("iPhone10,4") || deviceModel.Contains("iPhone10,5"))
				return PerformanceLevel.Medium;
			// iPhone SE (1st gen) (iPhone8,4)
			if (deviceModel.Contains("iPhone8,4"))
				return PerformanceLevel.Medium;
			// iPhone SE (2nd gen) (iPhone12,8)
			if (deviceModel.Contains("iPhone12,8"))
				return PerformanceLevel.High;
			// iPhone SE (3rd gen) (iPhone14,6)
			if (deviceModel.Contains("iPhone14,6"))
				return PerformanceLevel.High;
			return PerformanceLevel.High;
		}
#endif
		return PerformanceLevel.High;
	}
}
