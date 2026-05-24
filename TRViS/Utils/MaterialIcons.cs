namespace TRViS.Utils;

/// <summary>
/// Named codepoint constants for the Material Icons (Regular) font shipped via
/// MauiProgram.ConfigureFonts as alias <c>"MaterialIconsRegular"</c>.
/// <para>
/// Use these constants instead of raw glyphs in source. Pasting raw PUA chars
/// makes the codepoint invisible to anyone whose editor isn't rendering with
/// the Material Icons font, and an off-by-one paste (like U+E249 vs U+E0F2)
/// silently produces a missing-glyph fallback at runtime — exactly the bug
/// that surfaced for SampleDataLoader's "デモデータ" tile.
/// </para>
/// <para>
/// Codepoints come from
/// <c>https://github.com/google/material-design-icons/blob/master/font/MaterialIcons-Regular.codepoints</c>.
/// Add entries in ascending codepoint order so duplicates are easy to spot,
/// and always use \uXXXX escapes (never raw glyphs) so the value is visible
/// in any editor regardless of installed fonts.
/// </para>
/// </summary>
internal static class MaterialIcons
{
	// E1xx
	/// <summary>link — generic external resource fallback.</summary>
	public const string Link = "\uE157";

	/// <summary>storage — disk / database file (used for .sqlite/.db).</summary>
	public const string Storage = "\uE1DB";

	// E2xx — folder family
	/// <summary>folder — closed folder.</summary>
	public const string Folder = "\uE2C7";

	/// <summary>folder_off — empty-state illustration in SelectFileDialog.</summary>
	public const string FolderOff = "\uE2C4";

	/// <summary>drive_file_move — "保存場所を開く" footer affordance.</summary>
	public const string DriveFileMove = "\uE89E";

	// E3xx
	/// <summary>phone_iphone — trvis:// deeplink history glyph.</summary>
	public const string PhoneIphone = "\uE324";

	/// <summary>flash_on — realtime stream (ws/wss) history glyph.</summary>
	public const string FlashOn = "\uE3E7";

	// E5xx — directional / chrome
	/// <summary>arrow_back — back navigation.</summary>
	public const string ArrowBack = "\uE5C4";

	/// <summary>chevron_right — disclosure arrow.</summary>
	public const string ChevronRight = "\uE5CC";

	/// <summary>close — modal/dialog dismiss.</summary>
	public const string Close = "\uE5CD";

	/// <summary>arrow_upward — "go up one folder" affordance.</summary>
	public const string ArrowUpward = "\uE5D8";

	/// <summary>menu — flyout/drawer toggle.</summary>
	public const string Menu = "\uE5D2";

	// E6xx
	/// <summary>wifi — network sync / WebSocket-loaded data.</summary>
	public const string Wifi = "\uE63E";

	/// <summary>wifi_off \u2014 server sync connection lost (disconnected state).</summary>
	public const string WifiOff = "\uE648";

	// E8xx — content / files
	/// <summary>code — file with code/text content (used for .json files).</summary>
	public const string Code = "\uE86F";

	/// <summary>description — generic file (default loader info glyph).</summary>
	public const string Description = "\uE873";

	/// <summary>language — globe / web URL (https / http history glyph).</summary>
	public const string Language = "\uE894";

	/// <summary>settings \u2014 gear (OriginalTimetable V1 tweaks panel toggle).</summary>
	public const string Settings = "\uE8B8";

	// EAxx
	/// <summary>science — flask, used for the demo/sample data tile.</summary>
	public const string Science = "\uEA4B";
}
