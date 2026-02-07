using System.Globalization;

using Microsoft.Maui.Controls;

using TRViS.IO.Models;

namespace TRViS.CustomRoute.Converters;

/// <summary>
/// TimeData を HHMM 形式の文字列に変換するコンバーター
/// D-TAC の TimeCell と同様の処理を実装
/// </summary>
public class TimeDataConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not TimeData timeData)
			return string.Empty;

		// 時刻データがある場合
		if (timeData.Hour is not null || timeData.Minute is not null || timeData.Second is not null)
		{
			string hhmm = string.Empty;
			if (timeData.Hour is not null)
				hhmm += $"{timeData.Hour % 24}.";
			hhmm += $"{timeData.Minute ?? 0:D02}";
			return hhmm;
		}

		// テキストがある場合（↓ や その他）
		return timeData.Text ?? string.Empty;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
