using System.Globalization;

using TRViS.MyAppCustomizables;

#if IOS || MACCATALYST
using Foundation;
#endif

namespace TRViS.Localization;

/// <summary>
/// iOS / Mac Catalyst の「アプリ別の優先言語」(<c>AppleLanguages</c>) との橋渡し。
///
/// この OS では OS 側 (iOS設定 / システム設定) を言語の真実とし、アプリ内の
/// 言語Pickerは <c>AppleLanguages</c> を書き換える「ミラー」として振る舞う。
/// Android / Windows には同等の OS 項目が無いため、これらは何もしない
/// (<see cref="IsSupported"/> = false) ＝ 従来どおり JSON 設定で管理する。
/// </summary>
public static class AppLanguageOsBridge
{
#if IOS || MACCATALYST
	public static bool IsSupported => true;

	const string AppleLanguagesKey = "AppleLanguages";

	/// <summary>
	/// 現在 OS が解決している実効言語を Picker 表示用にマップする。
	/// 対応言語は en / ja のみ宣言しているため、実質どちらかに解決される。
	/// </summary>
	public static AppLanguage GetEffectiveLanguage()
		=> CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
		{
			"ja" => AppLanguage.Japanese,
			"en" => AppLanguage.English,
			_ => AppLanguage.System,
		};

	/// <summary>
	/// Picker の選択を OS (<c>AppleLanguages</c>) に書き戻す。
	/// <see cref="AppLanguage.System"/> はアプリ別オーバーライドを解除し、
	/// 端末全体の言語設定に従わせる。OS への完全反映は次回起動から
	/// (アプリ内表示は <see cref="LocalizationResourceManager"/> が即時更新)。
	/// </summary>
	public static void WriteOsOverride(AppLanguage language)
	{
		NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;
		switch (language)
		{
			case AppLanguage.Japanese:
				defaults[AppleLanguagesKey] = NSArray.FromStrings("ja");
				break;
			case AppLanguage.English:
				defaults[AppleLanguagesKey] = NSArray.FromStrings("en");
				break;
			default: // System: 端末設定に従わせるためアプリ別指定を消す
				defaults.RemoveObject(AppleLanguagesKey);
				break;
		}
		defaults.Synchronize();
	}
#else
	public static bool IsSupported => false;

	public static AppLanguage GetEffectiveLanguage() => AppLanguage.System;

	public static void WriteOsOverride(AppLanguage language) { /* no OS bridge */ }
#endif
}
