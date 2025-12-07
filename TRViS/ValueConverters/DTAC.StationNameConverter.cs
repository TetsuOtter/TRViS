using System.Globalization;

using TRViS.Utils;

namespace TRViS.ValueConverters.DTAC;

public class StationNameConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string s)
			return value;

		return StationNameConverter.Convert(s);
	}

	public static string Convert(string s)
		=> s.Length switch
		{
			2 => Util.InsertCharBetweenCharAndMakeWide(s, $"{Util.SPACE_CHAR}{Util.SPACE_CHAR}{Util.SPACE_CHAR}{Util.SPACE_CHAR}"),
			3 => Util.InsertCharBetweenCharAndMakeWide(s, Util.SPACE_CHAR),
#if IOS
			4 => DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Tablet
				? Util.InsertCharBetweenCharAndMakeWide(s, Util.THIN_SPACE)
				: s,
#endif
			_ => s
		};

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> (value as string)?.Replace(Util.SPACE_CHAR, string.Empty).Replace(Util.THIN_SPACE, string.Empty) ?? value;
}

