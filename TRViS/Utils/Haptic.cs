namespace TRViS;

public static partial class Util
{
	private static bool IsHapticEnabled { get; set; } = true;
	private static DateTime? LastHapticTime { get; set; } = null;
	public static void PerformHaptic(HapticFeedbackType type)
	{
		if (!IsHapticEnabled)
			return;
		if (LastHapticTime.HasValue && (DateTime.Now - LastHapticTime.Value).TotalMilliseconds < 50)
			return;
		try
		{
			HapticFeedback.Default.Perform(type);
			LastHapticTime = DateTime.Now;
		}
		catch (FeatureNotSupportedException)
		{
			IsHapticEnabled = false;
		}
		catch (Exception ex)
		{
			IsHapticEnabled = false;
			logger.Error(ex, "HapticFeedback Failed");
		}
	}
}
