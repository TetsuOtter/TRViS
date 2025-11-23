using System;
using System.Globalization;

using TRViS.Core;

namespace TRViS.ValueConverters.DTAC;

public class TrainNumberConverter : IValueConverter
{
	public static string Convert(in string s)
		=> s.Length switch
		{
			<= 6 => StringUtils.InsertCharBetweenCharAndMakeWide(s, StringUtils.SPACE_CHAR),
			<= 9 => StringUtils.InsertCharBetweenCharAndMakeWide(s, StringUtils.THIN_SPACE),
			_ => s
		};
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string s)
			return value;

		return Convert(s);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> (value as string)?.Replace(StringUtils.SPACE_CHAR, string.Empty).Replace(StringUtils.THIN_SPACE, string.Empty) ?? value;
}

