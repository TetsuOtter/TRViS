namespace TRViS.Services;

/// <summary>
/// Represents the allowed display orientations for a screen or page.
/// </summary>
public enum AppDisplayOrientation
{
	/// <summary>
	/// Allow all orientations (portrait and landscape).
	/// </summary>
	All,

	/// <summary>
	/// Lock to portrait orientation only.
	/// </summary>
	Portrait,

	/// <summary>
	/// Lock to landscape orientation only.
	/// </summary>
	Landscape
}
