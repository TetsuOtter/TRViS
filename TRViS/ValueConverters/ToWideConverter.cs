using System;
using System.Globalization;

namespace TRViS.ValueConverters;

public class ToWideConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is string s ? Utils.ToWide(s) : value;

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value;
}
