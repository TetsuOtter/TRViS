namespace TRViS.MyAppCustomizables;

/// <summary>
/// アプリのUI表示言語。<see cref="System"/> の場合は端末の言語設定に従う。
/// JSON 設定ファイルには数値で永続化される (他の設定 enum と同様)。
/// </summary>
public enum AppLanguage
{
	System = 0,
	Japanese = 1,
	English = 2,
}
