using System.Globalization;

namespace TRViS.ValueConverters;

public class BGColorToTextColorConverter : IValueConverter
{
	public static BGColorToTextColorConverter Default { get; } = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is not Color v ? value : Utils.GetTextColorFromBGColor(v);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
