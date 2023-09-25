using System.Globalization;

namespace TRViS.ValueConverters;

public class IsOneOrMoreIntConverter : IValueConverter
{
	static public IsOneOrMoreIntConverter Default { get; } = new();

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is int and >= 1;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
