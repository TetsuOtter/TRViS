namespace TRViS.DTAC.Logic.Formatters;

internal static class AffectDateFormatter
{
	public static string FormatAffectDate(DateTime? affectDate, int dayCount)
	{
		try
		{
			if (affectDate.HasValue)
				return affectDate.Value.ToString("yyyy年M月d日");
			return DateOnly.FromDateTime(DateTime.Now).AddDays(-dayCount).ToString("yyyy年M月d日");
		}
		catch
		{
			return string.Empty;
		}
	}

	public static string FormatAffectDateOnly(DateOnly? affectDate, int dayCount)
	{
		try
		{
			if (affectDate.HasValue)
				return affectDate.Value.ToString("yyyy年M月d日");
			return DateOnly.FromDateTime(DateTime.Now).AddDays(-dayCount).ToString("yyyy年M月d日");
		}
		catch
		{
			return string.Empty;
		}
	}
}
