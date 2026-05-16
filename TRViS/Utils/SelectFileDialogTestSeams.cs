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
	/// Syntactically-broken JSON written at the root of TimetableFileDirectory
	/// for the friendly-error test (issue #49). Loading it makes
	/// <c>JsonSerializer</c> throw a <c>JsonException</c>, which the new
	/// LoadErrorMessage path must turn into a readable alert.
	/// </summary>
	public const string MalformedJsonFileName = "ui-test-malformed.json";

	/// <summary>
	/// Minimal-but-non-empty WorkGroup payload. TWO WorkGroups (each one Work,
	/// no Trains). LoaderJson accepts this and the resulting Loader exposes a
	/// non-empty WorkGroupList — the load-success test asserts against that
	/// instead of just modal dismiss, so an "always returns true" regression
	/// in TryLoadFileAsync would still fail the test.
	///
	/// Two WorkGroups (not one) on purpose: TimetableSelectionManager auto-
	/// commits a *single* WorkGroup on load (zero-friction for the common
	/// 1-WG case), which makes the Home picker skip the WorkGroup-list step.
	/// These SelectFileDialog tests assert the post-load picker via
	/// IsWorkGroupListVisible, so they need a multi-WG fixture to keep the
	/// list step on screen. Single-WG auto-select itself is covered by
	/// TimetableSelectionManagerTests.OnLoaderChanged_AutoSelectsWhenSingleWorkGroup.
	/// </summary>
	private const string FixtureJson =
		"[{\"Id\":\"ui-test-wg\",\"Name\":\"UITest WG\",\"Works\":[" +
		"{\"Id\":\"ui-test-work\",\"Name\":\"UITest Work\",\"AffectDate\":\"20260101\",\"Trains\":[]}]}," +
		"{\"Id\":\"ui-test-wg2\",\"Name\":\"UITest WG2\",\"Works\":[" +
		"{\"Id\":\"ui-test-work2\",\"Name\":\"UITest Work2\",\"AffectDate\":\"20260101\",\"Trains\":[]}]}]";

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
	/// Writes a single syntactically-broken JSON file at the root of
	/// TimetableFileDirectory so a SelectFileDialog tap exercises the
	/// JsonException → friendly-alert path (issue #49). Wiped by
	/// <see cref="ClearSampleFiles"/> like every other seeded fixture.
	/// </summary>
	public static void SeedMalformedJson()
	{
		string root = DirectoryPathProvider.TimetableFileDirectory.FullName;
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, MalformedJsonFileName), "{ this is not valid json");
		logger.Info("SeedMalformedJson: root={0}", root);
	}

	/// <summary>
	/// Wipes everything under TimetableFileDirectory, clears any pending
	/// FilePicker override, and resets <see cref="ViewModels.AppViewModel.Loader"/>
	/// to null. Tests call this in SetUp because:
	///   1. iOS noReset:true and Mac Catalyst's app sandbox both keep the
	///      documents folder warm across sessions — without wiping, a single
	///      seeded JSON file from the previous test triggers
	///      DefaultTimetableFileLoader auto-load on launch, putting the next
	///      test in Home mode (SelectFileButton hidden).
	///   2. <see cref="FilePickerProvider.OverrideForTesting"/> is a static
	///      that survives Driver.Quit().
	///   3. Even after wiping the folder, an in-memory Loader from a previous
	///      test (e.g. TapFileCard or BrowseButton-fallback flows that
	///      successfully loaded) keeps the page in Home mode until the next
	///      app process starts. Reset it explicitly so the page flips back
	///      to Start mode immediately via the AppViewModel observer.
	/// </summary>
	public static void ClearSampleFiles()
	{
		FilePickerProvider.OverrideForTesting = null;

		// Reset the AppViewModel Loader so StartHomePage's mode-switch observer
		// flips back to Start mode (SelectFileButton + LoadDemoButton visible).
		// Has to run on the UI thread because SetLoader fires PropertyChanged
		// which the page handler subscribes to and runs animations on the UI
		// dispatcher. The seam button click handler is already on the UI
		// thread, so this is a direct call — no dispatcher hop needed.
		try
		{
			var viewModel = InstanceManager.AppViewModel;
			if (viewModel.Loader is not null)
			{
				logger.Info("ClearSampleFiles: resetting AppViewModel.Loader (was non-null)");
				viewModel.Loader?.Dispose();
				viewModel.SetLoader(null, null);
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "ClearSampleFiles: failed to reset Loader");
		}

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
