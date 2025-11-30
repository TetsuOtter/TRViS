namespace TRViS.IO;

public static class StringToDateOnlyUtil
{
	public static bool TryStringToDateOnly(string? value, out DateOnly date)
	{
		if (string.IsNullOrEmpty(value))
		{
			date = default;
			return false;
		}

		if (value.Length == 8 && value.All(char.IsDigit))
		{
			int year = int.Parse(value[..4]);
			int month = int.Parse(value[4..6]);
			int day = int.Parse(value[6..]);
			date = new DateOnly(year, month, day);
			return true;
		}

		return DateOnly.TryParse(value, out date);
	}

	public static DateOnly? StringToDateOnlyOrNull(string? value)
		=> TryStringToDateOnly(value, out DateOnly date) ? date : null;
}
