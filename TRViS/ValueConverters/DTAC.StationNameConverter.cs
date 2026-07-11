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

	// issue #41: 狭幅モードでは 4 文字駅名の細スペース挿入を抑え、列幅を節約する
	public static string Convert(string s, bool isNarrowMode = false)
		=> s.Length switch
		{
			2 => StringUtils.InsertCharBetweenCharAndMakeWide(s, $"{StringUtils.SPACE_CHAR}{StringUtils.SPACE_CHAR}{StringUtils.SPACE_CHAR}{StringUtils.SPACE_CHAR}"),
			3 => StringUtils.InsertCharBetweenCharAndMakeWide(s, StringUtils.SPACE_CHAR),
			// 全環境共通で細スペースを挿入する (プラットフォーム限定だと4文字駅名が
			// カラムからはみ出して折り返す — 詳細は d571884)。狭幅モードのみ、
			// 列幅節約のため挿入を抑える (issue #41)。
			4 => isNarrowMode ? s : StringUtils.InsertCharBetweenCharAndMakeWide(s, StringUtils.THIN_SPACE),
			_ => s
		};

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> (value as string)?.Replace(StringUtils.SPACE_CHAR, string.Empty).Replace(StringUtils.THIN_SPACE, string.Empty) ?? value;
}

