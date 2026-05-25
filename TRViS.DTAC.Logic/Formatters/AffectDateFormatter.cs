namespace TRViS.DTAC.Logic.Formatters;

public static class AffectDateFormatter
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

	/// <summary>
	/// 「施行日」表示用のフォーマット。
	/// <paramref name="overrideText"/> に非空の文字列が渡された場合、そのまま採用する
	/// (= 日付として解釈できない任意文字列を表示できる)。
	/// 渡されなかった場合は <see cref="FormatAffectDateOnly"/> と同等のフォールバック動作。
	/// </summary>
	public static string FormatAffectDateOrText(string? overrideText, DateOnly? affectDate, int dayCount)
	{
		if (!string.IsNullOrEmpty(overrideText))
			return overrideText;
		return FormatAffectDateOnly(affectDate, dayCount);
	}
}
