namespace TRViS;

public static partial class Utils
{
	private static bool IsHapticEnabled { get; set; } = true;
	public static void PerformHaptic(HapticFeedbackType type)
	{
		if (!IsHapticEnabled)
			return;
		try
		{
			HapticFeedback.Default.Perform(type);
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
