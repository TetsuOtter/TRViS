using System.Globalization;

using TRViS.Core;

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
			2 => StringUtils.InsertCharBetweenCharAndMakeWide(s, $"{StringUtils.SPACE_CHAR}{StringUtils.SPACE_CHAR}{StringUtils.SPACE_CHAR}{StringUtils.SPACE_CHAR}"),
			3 => StringUtils.InsertCharBetweenCharAndMakeWide(s, StringUtils.SPACE_CHAR),
#if IOS
			4 => DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Tablet
				? StringUtils.InsertCharBetweenCharAndMakeWide(s, StringUtils.THIN_SPACE)
				: s,
#endif
			_ => s
		};

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> (value as string)?.Replace(StringUtils.SPACE_CHAR, string.Empty).Replace(StringUtils.THIN_SPACE, string.Empty) ?? value;
}

