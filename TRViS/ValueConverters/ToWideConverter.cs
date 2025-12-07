using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using TRViS.Utils;

namespace TRViS.ValueConverters;

public class ToWideConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is string s ? Convert(s) : value;

	[return: NotNullIfNotNull(nameof(value))]
	public static string? Convert(string? value)
		=> value is null ? null : Util.ToWide(value);

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value;
}
