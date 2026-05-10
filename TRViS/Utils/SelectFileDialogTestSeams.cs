#if UI_TEST
using TRViS.Services;

namespace TRViS.Utils;

/// <summary>
/// UI_TEST-only helpers that prepare on-disk state and the FilePicker override
/// for <see cref="TRViS.RootPages.SelectFileDialog"/> tests. Whole class is
/// behind <c>#if UI_TEST</c> so it never ships in release builds.
///
/// Mirrors the <see cref="ViewModels.AppViewModel.SeedUrlHistoryForTesting"/>
/// pattern: the StartHomePage exposes hidden seam buttons that call into here,
/// so tests can populate fixtures without driving SendKeys / the OS picker.
/// </summary>
internal static class SelectFileDialogTestSeams
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	// ----- Seeded fixture file names ------------------------------------------
	// Public so the test page-object can assert against them by name without
	// duplicating the literal in two places.

	/// <summary>JSON file written at the root of TimetableFileDirectory.</summary>
	public const string RootSampleFileName = "ui-test-root.json";
	/// <summary>Sub-folder name created under TimetableFileDirectory.</summary>
	public const string SubFolderName = "ui-test-folder";
	/// <summary>JSON file written inside <see cref="SubFolderName"/>.</summary>
	public const string NestedSampleFileName = "ui-test-nested.json";
	/// <summary>
	/// JSON file written outside TimetableFileDirectory for the Browse-fallback
	/// test. Lives in <see cref="FileSystem.CacheDirectory"/> so it is reachable
	/// from the app process but does not pollute the in-app file list.
	/// </summary>
	public const string BrowseFallbackFileName = "ui-test-browse-fallback.json";

	/// <summary>
	/// Minimal-but-non-empty WorkGroup payload. One WorkGroup, one Work, no
	/// Trains. LoaderJson accepts this and the resulting Loader exposes a
	/// non-empty WorkGroupList — the load-success test asserts against that
	/// instead of just modal dismiss, so an "always returns true" regression
	/// in TryLoadFileAsync would still fail the test.
	/// </summary>
	private const string FixtureJson =
		"[{\"Id\":\"ui-test-wg\",\"Name\":\"UITest WG\",\"Works\":[" +
		"{\"Id\":\"ui-test-work\",\"Name\":\"UITest Work\",\"AffectDate\":\"20260101\",\"Trains\":[]}]}]";

	/// <summary>
	/// Writes the canonical SelectFileDialog fixture into TimetableFileDirectory:
	/// one JSON at the root, one sub-folder containing a second JSON. Idempotent —
	/// safe to call before every drill-down / file-load test.
	/// </summary>
	public static void SeedSampleFiles()
	{
		string root = DirectoryPathProvider.TimetableFileDirectory.FullName;
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, RootSampleFileName), FixtureJson);

		string sub = Path.Combine(root, SubFolderName);
		Directory.CreateDirectory(sub);
		File.WriteAllText(Path.Combine(sub, NestedSampleFileName), FixtureJson);

		logger.Info("SeedSampleFiles: root={0} sub={1}", root, sub);
	}

	/// <summary>
	/// Wipes everything under TimetableFileDirectory and clears any pending
	/// FilePicker override. Tests call this in SetUp because iOS noReset:true
	/// means the documents folder persists between sessions, and the
	/// <see cref="FilePickerProvider.OverrideForTesting"/> static survives
	/// Driver.Quit() if the OS process stays warm.
	/// </summary>
	public static void ClearSampleFiles()
	{
		FilePickerProvider.OverrideForTesting = null;

		var dir = DirectoryPathProvider.TimetableFileDirectory;
		if (!dir.Exists)
		{
			logger.Info("ClearSampleFiles: {0} does not exist — no-op", dir.FullName);
			return;
		}

		try
		{
			foreach (FileInfo f in dir.GetFiles("*", SearchOption.AllDirectories))
			{
				try { f.Delete(); }
				catch (Exception ex) { logger.Warn(ex, "ClearSampleFiles: delete file failed: {0}", f.FullName); }
			}
			// Delete sub-directories bottom-up so a non-empty parent doesn't
			// fail to delete because we walked it before its children.
			foreach (DirectoryInfo d in dir.GetDirectories("*", SearchOption.AllDirectories)
				.OrderByDescending(d => d.FullName.Length))
			{
				try { d.Delete(recursive: true); }
				catch (Exception ex) { logger.Warn(ex, "ClearSampleFiles: delete dir failed: {0}", d.FullName); }
			}
			logger.Info("ClearSampleFiles: cleared {0}", dir.FullName);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "ClearSampleFiles failed");
		}

		// Also remove any stale browse-fallback file from previous runs.
		string fallbackPath = Path.Combine(FileSystem.CacheDirectory, BrowseFallbackFileName);
		try
		{
			if (File.Exists(fallbackPath))
				File.Delete(fallbackPath);
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "ClearSampleFiles: delete fallback file failed: {0}", fallbackPath);
		}
	}

	/// <summary>
	/// Writes a JSON fixture into <see cref="FileSystem.CacheDirectory"/> and
	/// installs a <see cref="FilePickerProvider.OverrideForTesting"/> that
	/// returns its path. The next "他の場所からファイルを開く" tap then runs
	/// the real load path with no OS picker dialog. Returns the full path so
	/// the seam button handler can log it.
	/// </summary>
	public static string SetupBrowseFallback()
	{
		string fallbackPath = Path.Combine(FileSystem.CacheDirectory, BrowseFallbackFileName);
		Directory.CreateDirectory(FileSystem.CacheDirectory);
		File.WriteAllText(fallbackPath, FixtureJson);
		FilePickerProvider.OverrideForTesting = () => Task.FromResult<FileResult?>(new FileResult(fallbackPath));
		logger.Info("SetupBrowseFallback: {0}", fallbackPath);
		return fallbackPath;
	}
}
#endif
