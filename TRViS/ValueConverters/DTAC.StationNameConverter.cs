using System;
using System.Globalization;

namespace TRViS.ValueConverters.DTAC;

public class StationNameConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not string s)
			return value;

		return s.Length switch
		{
			2 => Utils.InsertCharBetweenCharAndMakeWide(s, $"{Utils.SPACE_CHAR}{Utils.SPACE_CHAR}{Utils.SPACE_CHAR}{Utils.SPACE_CHAR}"),
			3 => Utils.InsertCharBetweenCharAndMakeWide(s, Utils.SPACE_CHAR),
			4 => Utils.InsertCharBetweenCharAndMakeWide(s, Utils.THIN_SPACE),
			_ => s
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> (value as string)?.Replace(Utils.SPACE_CHAR, string.Empty).Replace(Utils.THIN_SPACE, string.Empty) ?? value;
}

