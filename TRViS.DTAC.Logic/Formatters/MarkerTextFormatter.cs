using System.Globalization;

namespace TRViS.DTAC.Logic.Formatters;

/// <summary>
/// Formats marker text by limiting it to the equivalent of 4 half-width characters
/// (2 full-width characters). Full-width characters count as 2, half-width as 1.
/// </summary>
public static class MarkerTextFormatter
{
	/// <summary>
	/// Limits the marker text to 4 half-width character equivalents (2 full-width).
	/// Returns the original value if it fits; returns a truncated string if it exceeds the limit.
	/// Null or empty input is returned as-is.
	/// </summary>
	public static string? LimitMarkerText(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return text;

		var elementEnumerator = StringInfo.GetTextElementEnumerator(text);
		int width = 0;
		int charIndex = 0;

		while (elementEnumerator.MoveNext())
		{
			string element = elementEnumerator.GetTextElement();

			// 文字の幅を判定（全角は2、半角は1）
			int elementWidth = IsFullWidth(element) ? 2 : 1;

			if (width + elementWidth > 4)
			{
				// 4文字相当を超えるので切断
				return text.Substring(0, charIndex);
			}

			width += elementWidth;
			charIndex += element.Length;
		}

		return text;
	}

	/// <summary>
	/// Determines whether the first character of the text is a full-width character
	/// (CJK ideographs, hiragana, katakana, etc.).
	/// </summary>
	public static bool IsFullWidth(string text)
	{
		if (string.IsNullOrEmpty(text))
			return false;

		// 最初の文字のUnicodeカテゴリを確認
		char ch = text[0];

		// 一般的な全角文字の判定
		// ひらがな、カタカナ、漢字、全角記号など
		return char.GetUnicodeCategory(ch) switch
		{
			UnicodeCategory.OtherLetter => true,      // CJK文字など
			UnicodeCategory.OtherSymbol => true,       // 全角記号
			UnicodeCategory.OtherPunctuation => true,  // 全角句読点
			_ => false
		};
	}
}
