using System.Globalization;

namespace TRViS.ValueConverters;

public class DoubleToIntConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> (int?)value ?? 0;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> value;
}

