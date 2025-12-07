using System;
using System.Globalization;

namespace TRViS.ValueConverters.DTAC;

public class TrainNumberConverter : IValueConverter
{
	public static string Convert(in string s)
		=> s.Length switch
		{
			<= 6 => Util.InsertCharBetweenCharAndMakeWide(s, Util.SPACE_CHAR),
			<= 9 => Util.InsertCharBetweenCharAndMakeWide(s, Util.THIN_SPACE),
			_ => s
		};
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string s)
			return value;

		return Convert(s);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> (value as string)?.Replace(Util.SPACE_CHAR, string.Empty).Replace(Util.THIN_SPACE, string.Empty) ?? value;
}

