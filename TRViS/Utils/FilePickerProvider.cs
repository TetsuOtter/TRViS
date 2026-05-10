namespace TRViS.Utils;

/// <summary>
/// Indirection over <see cref="FilePicker.Default"/> so the post-pick load path
/// can be exercised in UI tests without driving the real OS file picker (which
/// is system UI and out of Appium's reach on every platform we target).
///
/// Production path is unchanged: <see cref="PickAsync"/> just delegates to
/// <see cref="FilePicker.Default"/>. The override hook is gated behind
/// <c>UI_TEST</c> so the field doesn't ship in release builds at all.
/// </summary>
internal static class FilePickerProvider
{
#if UI_TEST
	/// <summary>
	/// UI_TEST-only override. When non-null, <see cref="PickAsync"/> returns
	/// this delegate's result instead of invoking <see cref="FilePicker.Default"/>.
	/// Tests set this to a delegate that returns a <see cref="FileResult"/>
	/// pointing at a known seeded file, then tap the "他の場所からファイルを開く"
	/// button — the dialog runs the real load+dismiss path with no OS picker.
	/// Static lifetime mirrors <see cref="FilePicker.Default"/>; tests should
	/// null this out via the dedicated reset seam between scenarios.
	/// </summary>
	public static Func<Task<FileResult?>>? OverrideForTesting { get; set; }
#endif

	public static Task<FileResult?> PickAsync()
	{
#if UI_TEST
		if (OverrideForTesting is { } overrideFn)
			return overrideFn();
#endif
		return FilePicker.Default.PickAsync();
	}
}
