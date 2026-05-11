using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Microsoft.Maui.Controls.Shapes;

using TRViS.IO;
using TRViS.Services;
using TRViS.Utils;

using IOSPage = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page;

namespace TRViS.RootPages;

/// <summary>
/// Modal page used by Start/Home → "ファイルを選択". Lists JSON / SQLite
/// files from <see cref="DirectoryPathProvider.TimetableFileDirectory"/>
/// (and its sub-folders, drill-down style) as rich cards. Includes an
/// "他の場所からファイルを開く" button that falls back to the OS file picker,
/// and "保存場所を開く" that reveals the documents folder in
/// the OS file manager.
/// </summary>
public partial class SelectFileDialog : ContentPage
{
	internal const string AutomationId_FileItemPrefix = "SelectFile.FileItem.";
	internal const string AutomationId_FolderItemPrefix = "SelectFile.FolderItem.";
	internal const string AutomationId_UpFolderItem = "SelectFile.UpFolderItem";

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	// File extensions recognized by the in-app card list. SQLite triplet matches
	// the OnSelectFileClicked dispatch in StartHomePage.xaml.cs (.sqlite/.db/.sqlite3).
	private static readonly string[] s_listedExtensions = { ".json", ".sqlite", ".db", ".sqlite3" };

	// OS file picker filter. Strings are interpreted per-platform:
	//   - iOS / MacCatalyst: UTI identifiers passed to UIDocumentPickerViewController.
	//     `public.json` is system-defined; `public.database` covers files tagged as
	//     databases. Note: `.sqlite/.db/.sqlite3` files are not natively tagged as
	//     `public.database` on iOS — they appear as `public.data`. Until UTI conformance
	//     is declared in Info.plist (UTExportedTypeDeclarations), iOS users who need to
	//     pick a sqlite/db file should tap "..." → "Show all files" in the picker. The
	//     post-pick extension check in TryLoadFileAsync remains the safety net.
	//   - Android: MIME types. `application/octet-stream` is intentionally included
	//     because Android often misidentifies user-supplied sqlite files with that MIME.
	//   - Windows: file extensions including the leading dot.
	private static readonly PickOptions s_pickOptions = new()
	{
		FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
		{
			{ DevicePlatform.iOS, new[] { "public.json", "public.database" } },
			{ DevicePlatform.MacCatalyst, new[] { "public.json", "public.database" } },
			{ DevicePlatform.Android, new[] { "application/json", "application/x-sqlite3", "application/vnd.sqlite3", "application/octet-stream" } },
			{ DevicePlatform.WinUI, new[] { ".json", ".sqlite", ".db", ".sqlite3" } },
		}),
	};

	// Drill-down state. _rootDirectory is the user-visible "top" — going above it
	// is not allowed (we don't want users wandering into the rest of the sandbox).
	private DirectoryInfo _rootDirectory = DirectoryPathProvider.TimetableFileDirectory;
	private DirectoryInfo _currentDirectory = DirectoryPathProvider.TimetableFileDirectory;
	// Re-entrancy guard for card / browse / open-storage taps. SetInputEnabled
	// disables the Buttons but TapGestureRecognizers on Border cards stay live —
	// a second tap during an in-flight load could queue a duplicate
	// TryLoadFileAsync, race on viewModel.SetLoader, and PopModalAsync after
	// the page is already disposed. The flag short-circuits at the top of every
	// tap handler.
	private bool _isBusy;
	// Tracks whether OnAppearing has already initialised drill-down state. The
	// OS file picker (Browse) and unsupported-extension alert each re-fire
	// OnAppearing on dismissal, and unconditionally resetting _currentDirectory
	// would bounce the user back to the root — losing the folder they had
	// drilled into. Only reset on the first appearance.
	private bool _initialAppearanceDone;

	public SelectFileDialog()
	{
		logger.Trace("Creating");
		InitializeComponent();

		// iPad / Mac Catalyst: present as centered FormSheet so the card list
		// doesn't take the whole screen. iPhone falls back to fullscreen
		// automatically because UIKit ignores FormSheet on compact widths.
		IOSPage.SetModalPresentationStyle(this.On<iOS>(), UIModalPresentationStyle.FormSheet);

		// Set initial sub-view visibility BEFORE first paint. The XAML defaults
		// elements to IsVisible=true so each gets a UIA peer on Windows (where
		// IsVisible='False' XAML defaults skip peer creation and miss the
		// runtime flip), but BreadcrumbBorder and EmptyStateView would visually
		// overlap FileListView if all three stayed visible. Hide them
		// synchronously here; OnAppearing → PopulateFileList toggles them based
		// on the actual directory contents.
		BreadcrumbBorder.IsVisible = false;
		EmptyStateView.IsVisible = false;

		// "保存場所を開く" is hidden on Android: TimetableFileDirectory lives under
		// AppDataDirectory (/data/data/<pkg>/files/...), which is internal storage no
		// Files-app can browse — and a `file://` URI would throw FileUriExposedException
		// on API 24+. Until/unless the timetable directory is relocated to a shared
		// location (e.g. via SAF / scoped storage), opening it from Android is a no-op
		// at best. The button stays for iOS / Mac Catalyst / Windows where it works.
		if (DeviceInfo.Current.Platform == DevicePlatform.Android)
			OpenStorageLocationButton.IsVisible = false;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (!_initialAppearanceDone)
		{
			_rootDirectory = DirectoryPathProvider.TimetableFileDirectory;
			_currentDirectory = _rootDirectory;
			_initialAppearanceDone = true;
		}
		else
		{
			// Subsequent re-appearance (e.g. after an OS picker dismiss): refresh
			// the listing in case files were added/removed externally, but keep
			// the user's drill-down position. If the directory has been deleted
			// out from under us, fall back to root rather than render nothing.
			_currentDirectory.Refresh();
			if (!_currentDirectory.Exists)
				_currentDirectory = _rootDirectory;
		}
		PopulateFileList();
	}

	void PopulateFileList()
	{
		FileListContainer.Children.Clear();

		(DirectoryInfo[] folders, FileInfo[] files) = EnumerateCurrentDirectory();

		bool isRoot = IsAtRoot();
		BreadcrumbBorder.IsVisible = !isRoot;
		BreadcrumbLabel.Text = BuildBreadcrumb();

		if (folders.Length == 0 && files.Length == 0)
		{
			FileListView.IsVisible = false;
			EmptyStateView.IsVisible = true;
			return;
		}

		// "上の階層へ" card — first row when not at root, so navigation is reachable
		// even on small screens without scrolling.
		if (!isRoot)
			FileListContainer.Children.Add(CreateUpFolderCard());

		foreach (DirectoryInfo folder in folders)
			FileListContainer.Children.Add(CreateFolderCard(folder));

		foreach (FileInfo file in files)
			FileListContainer.Children.Add(CreateFileCard(file));

		FileListView.IsVisible = true;
		EmptyStateView.IsVisible = false;
	}

	bool IsAtRoot()
	{
		string current = NormalizePath(_currentDirectory.FullName);
		string root = NormalizePath(_rootDirectory.FullName);
		return string.Equals(current, root, StringComparison.OrdinalIgnoreCase);
	}

	string BuildBreadcrumb()
	{
		string current = NormalizePath(_currentDirectory.FullName);
		string root = NormalizePath(_rootDirectory.FullName);
		if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
			return _rootDirectory.Name;

		// Show "<root>/<relative path>". Path.GetRelativePath gives us the
		// relative segment with the platform's separator; we normalise to "/"
		// for consistent display across platforms.
		string rel = System.IO.Path.GetRelativePath(root, current).Replace(System.IO.Path.DirectorySeparatorChar, '/');
		return $"{_rootDirectory.Name}/{rel}";
	}

	static string NormalizePath(string path)
		=> path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

	(DirectoryInfo[] folders, FileInfo[] files) EnumerateCurrentDirectory()
	{
		try
		{
			// DirectoryInfo.Exists caches the result of the very first access for
			// the lifetime of the instance, and DirectoryPathProvider.TimetableFileDirectory
			// is a static, shared instance. On Android/Linux the static `_dataInitialised`
			// cache stays at "False" once stamped — even after Directory.CreateDirectory
			// runs (the create call doesn't refresh nearby DirectoryInfo handles), so a
			// freshly-created or freshly-seeded directory looks empty here. Refresh
			// invalidates the cache before the read so we always observe the current
			// filesystem state. (Reproduced in CI: SeededSqlite_AppearsInFileListView
			// on Android without this refresh — file present on disk per logcat, but
			// the dialog rendered empty state.)
			_currentDirectory.Refresh();
			if (!_currentDirectory.Exists)
				return (Array.Empty<DirectoryInfo>(), Array.Empty<FileInfo>());

			// Hide both Windows-style hidden attribute AND POSIX-style dot-prefixed
			// names (.DS_Store, ._foo.json, .git, etc.). FileAttributes.Hidden alone
			// catches almost nothing on iOS/macOS/Linux because the file system
			// convention there is name-based, not attribute-based.
			DirectoryInfo[] folders = _currentDirectory.GetDirectories()
				.Where(d => !d.Name.StartsWith('.') && (d.Attributes & FileAttributes.Hidden) == 0)
				.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
				.ToArray();

			FileInfo[] files = _currentDirectory.GetFiles()
				.Where(f => !f.Name.StartsWith('.')
					&& (f.Attributes & FileAttributes.Hidden) == 0
					&& s_listedExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
				.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
				.ToArray();

			return (folders, files);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "EnumerateCurrentDirectory failed: {0}", _currentDirectory.FullName);
			return (Array.Empty<DirectoryInfo>(), Array.Empty<FileInfo>());
		}
	}

	View CreateUpFolderCard()
	{
		var (border, grid) = CreateCardShell(AutomationId_UpFolderItem);

		var glyphLabel = NewGlyphLabel(MaterialIcons.ArrowUpward);
		Grid.SetColumn(glyphLabel, 0);
		Grid.SetRowSpan(glyphLabel, 2);
		grid.Children.Add(glyphLabel);

		var titleLabel = NewTitleLabel("上の階層へ");
		Grid.SetColumn(titleLabel, 1);
		Grid.SetRow(titleLabel, 0);
		grid.Children.Add(titleLabel);

		AttachTap(border, () =>
		{
			var parent = _currentDirectory.Parent;
			if (parent is null || IsAtRoot())
				return Task.CompletedTask;
			_currentDirectory = parent;
			PopulateFileList();
			return Task.CompletedTask;
		});

		return border;
	}

	View CreateFolderCard(DirectoryInfo folder)
	{
		var (border, grid) = CreateCardShell(AutomationId_FolderItemPrefix + folder.Name);

		var glyphLabel = NewGlyphLabel(MaterialIcons.Folder);
		Grid.SetColumn(glyphLabel, 0);
		Grid.SetRowSpan(glyphLabel, 2);
		grid.Children.Add(glyphLabel);

		var titleLabel = NewTitleLabel(folder.Name);
		Grid.SetColumn(titleLabel, 1);
		Grid.SetRow(titleLabel, 0);
		grid.Children.Add(titleLabel);

		var subtitleLabel = NewSubtitleLabel(SummarizeFolderContents(folder));
		Grid.SetColumn(subtitleLabel, 1);
		Grid.SetRow(subtitleLabel, 1);
		grid.Children.Add(subtitleLabel);

		string fullPath = folder.FullName;
		AttachTap(border, () =>
		{
			_currentDirectory = new DirectoryInfo(fullPath);
			PopulateFileList();
			return Task.CompletedTask;
		});

		return border;
	}

	static string SummarizeFolderContents(DirectoryInfo folder)
	{
		try
		{
			int folderCount = folder.GetDirectories().Count(d => (d.Attributes & FileAttributes.Hidden) == 0);
			int fileCount = folder.GetFiles()
				.Count(f => s_listedExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase));

			if (folderCount == 0 && fileCount == 0)
				return "(空)";
			var parts = new List<string>(2);
			if (folderCount > 0)
				parts.Add($"フォルダ {folderCount}");
			if (fileCount > 0)
				parts.Add($"ファイル {fileCount}");
			return string.Join(" ・ ", parts);
		}
		catch
		{
			// Permission errors etc — fall back to a neutral subtitle so the card still renders.
			return "";
		}
	}

	View CreateFileCard(FileInfo file)
	{
		string glyph = file.Extension.ToLowerInvariant() switch
		{
			".json" => MaterialIcons.Code,
			".sqlite" or ".db" or ".sqlite3" => MaterialIcons.Storage,
			_ => MaterialIcons.Description,
		};

		var (border, grid) = CreateCardShell(AutomationId_FileItemPrefix + file.Name);

		var glyphLabel = NewGlyphLabel(glyph);
		Grid.SetColumn(glyphLabel, 0);
		Grid.SetRowSpan(glyphLabel, 2);
		grid.Children.Add(glyphLabel);

		var titleLabel = NewTitleLabel(file.Name);
		Grid.SetColumn(titleLabel, 1);
		Grid.SetRow(titleLabel, 0);
		grid.Children.Add(titleLabel);

		var subtitleLabel = NewSubtitleLabel(FormatFileSize(file.Length));
		Grid.SetColumn(subtitleLabel, 1);
		Grid.SetRow(subtitleLabel, 1);
		grid.Children.Add(subtitleLabel);

		string fullPath = file.FullName;
		AttachTap(border, () => LoadFromCardAsync(fullPath));

		return border;
	}

	(Border border, Grid grid) CreateCardShell(string automationId)
	{
		var border = new Border
		{
			Style = (Style)Resources["FileCard"],
			AutomationId = automationId,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
		};

		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition { Width = GridLength.Auto },
				new ColumnDefinition { Width = GridLength.Star },
			},
			ColumnSpacing = 12,
			RowDefinitions =
			{
				new RowDefinition { Height = GridLength.Auto },
				new RowDefinition { Height = GridLength.Auto },
			},
		};
		border.Content = grid;
		return (border, grid);
	}

	static Label NewGlyphLabel(string glyph)
	{
		var label = new Label
		{
			Text = glyph,
			FontFamily = "MaterialIconsRegular",
			FontSize = 28,
			VerticalOptions = LayoutOptions.Center,
		};
		RootStyles.TableTextColor.Apply(label, Label.TextColorProperty);
		return label;
	}

	static Label NewTitleLabel(string text)
	{
		var label = new Label
		{
			Text = text,
			FontSize = 15,
			FontAttributes = FontAttributes.Bold,
			LineBreakMode = LineBreakMode.TailTruncation,
		};
		RootStyles.TableTextColor.Apply(label, Label.TextColorProperty);
		return label;
	}

	static Label NewSubtitleLabel(string text)
	{
		var label = new Label
		{
			Text = text,
			FontSize = 12,
			LineBreakMode = LineBreakMode.TailTruncation,
		};
		RootStyles.TableDetailColor.Apply(label, Label.TextColorProperty);
		return label;
	}

	static void AttachTap(Border border, Func<Task> handler)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, __) =>
		{
			try
			{ await handler(); }
			catch (Exception ex) { logger.Error(ex, "Card tap handler failed"); }
		};
		border.GestureRecognizers.Add(tap);
	}

	static string FormatFileSize(long bytes)
	{
		const long KB = 1024;
		const long MB = KB * 1024;
		if (bytes < KB)
			return $"{bytes} B";
		if (bytes < MB)
			return $"{bytes / (double)KB:F1} KB";
		return $"{bytes / (double)MB:F1} MB";
	}

	void SetInputEnabled(bool isEnabled)
	{
		_isBusy = !isEnabled;
		BrowseButton.IsEnabled = isEnabled;
		OpenStorageLocationButton.IsEnabled = isEnabled;
		LoadingIndicator.IsRunning = !isEnabled;
		LoadingIndicator.IsVisible = !isEnabled;
	}

	async Task LoadFromCardAsync(string fullPath)
	{
		if (_isBusy)
			return;
		logger.Info("Loading from card: {0}", fullPath);
		try
		{
			SetInputEnabled(false);
			bool ok = await TryLoadFileAsync(fullPath);
			if (ok)
				await Navigation.PopModalAsync();
		}
		finally
		{
			SetInputEnabled(true);
		}
	}

	async Task<bool> TryLoadFileAsync(string fullPath)
	{
		var viewModel = InstanceManager.AppViewModel;
		ILoader? lastLoader = viewModel.Loader;
		try
		{
			ILoader? newLoader;
			string ext = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();
			if (ext == ".json")
			{
				newLoader = await LoaderJson.InitFromFileAsync(fullPath);
			}
			else if (ext is ".sqlite" or ".db" or ".sqlite3")
			{
				newLoader = await LoaderSQL.CreateAsync(fullPath);
			}
			else
			{
				logger.Warn("Unknown file type: {0}", ext);
				await Util.DisplayAlertAsync("読み込めませんでした", "対応していないファイル形式です。", "OK");
				return false;
			}

			viewModel.SetLoader(newLoader, System.IO.Path.GetFileName(fullPath));
			if (!ReferenceEquals(lastLoader, viewModel.Loader))
				lastLoader?.Dispose();
			return true;
		}
		catch (Exception ex)
		{
			// SetLoader is the only mutation; if we throw before reaching it,
			// viewModel.Loader still references lastLoader and no rollback is
			// needed. (A prior version had a rollback block here, but with the
			// current control flow it was dead code.)
			logger.Error(ex, "TryLoadFileAsync failed: {0}", fullPath);
			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectFileDialog.TryLoadFileAsync");
			await Util.DisplayAlertAsync("読み込めませんでした", $"ファイルの読み込みに失敗しました: {ex.Message}", "OK");
			return false;
		}
	}

	// ----- Event handlers -----

	async void OnCloseClicked(object sender, EventArgs e)
	{
		logger.Trace("Close clicked");
		try
		{
			await Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PopModalAsync failed");
		}
	}

	async void OnBrowseClicked(object sender, EventArgs e)
	{
		logger.Info("Browse (FilePicker) clicked");
		try
		{
			SetInputEnabled(false);
			var result = await FilePicker.Default.PickAsync(s_pickOptions);
			if (result is null)
			{
				logger.Info("File picker cancelled");
				return;
			}

			logger.Info("File selected via picker: {0}", result.FullPath);

			// FileResult.FullPath on Android may be a content:// URI rather than
			// a filesystem path (per .NET MAUI FilePicker docs). The downstream
			// loaders use File.OpenRead / SqliteConnection, which require a real
			// path and would throw FileNotFoundException on a content:// URI.
			// Stream the picked file via FileResult.OpenReadAsync (works on every
			// platform) into the sandbox before handing the local path to the
			// loader. Landing under TimetableFileDirectory/imported/ also makes
			// the file appear in the card list on subsequent opens — the user
			// suggested UserContentsDirectory but only the timetables tree is
			// surfaced by the card list.
			string localPath = await ImportPickedFileAsync(result);

			bool ok = await TryLoadFileAsync(localPath);
			if (ok)
				await Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "FilePicker failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectFileDialog.OnBrowseClicked");
			await Util.DisplayAlertAsync("ファイルを開けませんでした", ex.Message, "OK");
		}
		finally
		{
			SetInputEnabled(true);
		}
	}

	static async Task<string> ImportPickedFileAsync(FileResult result)
	{
		DirectoryInfo importedDir = new(System.IO.Path.Combine(DirectoryPathProvider.TimetableFileDirectory.FullName, "imported"));
		if (!importedDir.Exists)
			importedDir.Create();

		string fileName = SanitizeFileName(result.FileName);
		string destPath = ResolveUniqueFilePath(importedDir.FullName, fileName);

		using Stream src = await result.OpenReadAsync();
		using FileStream dst = File.Create(destPath);
		await src.CopyToAsync(dst);

		return destPath;
	}

	static string SanitizeFileName(string name)
	{
		string fileName = System.IO.Path.GetFileName(name ?? string.Empty);
		if (string.IsNullOrWhiteSpace(fileName))
			return "imported_file";

		char[] invalid = System.IO.Path.GetInvalidFileNameChars();
		var sb = new System.Text.StringBuilder(fileName.Length);
		foreach (char ch in fileName)
			sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);

		string sanitized = sb.ToString().Trim();
		return string.IsNullOrEmpty(sanitized) ? "imported_file" : sanitized;
	}

	static string ResolveUniqueFilePath(string directory, string fileName)
	{
		string candidate = System.IO.Path.Combine(directory, fileName);
		if (!File.Exists(candidate))
			return candidate;

		string baseName = System.IO.Path.GetFileNameWithoutExtension(fileName);
		string ext = System.IO.Path.GetExtension(fileName);
		for (int i = 1; ; i++)
		{
			candidate = System.IO.Path.Combine(directory, $"{baseName} ({i}){ext}");
			if (!File.Exists(candidate))
				return candidate;
		}
	}

	async void OnOpenStorageLocationClicked(object sender, EventArgs e)
	{
		logger.Info("Open storage location clicked");
		try
		{
			// Make sure the directory exists so the OS file manager has somewhere to land.
			if (!_rootDirectory.Exists)
			{
				Directory.CreateDirectory(_rootDirectory.FullName);
				_rootDirectory.Refresh();
			}

			await OpenInOsFileManagerAsync(_rootDirectory.FullName);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "OnOpenStorageLocationClicked failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "SelectFileDialog.OnOpenStorageLocationClicked");
			await Util.DisplayAlertAsync("フォルダを開けませんでした", ex.Message, "OK");
		}
	}

	static Task OpenInOsFileManagerAsync(string fullPath)
	{
#if IOS && !MACCATALYST
		// iOS Files.app: requires UIFileSharingEnabled + LSSupportsOpeningDocumentsInPlace
		// in Info.plist (already set so the user can browse the documents folder).
		// The shareddocuments:// scheme jumps straight into the app's documents folder.
		// Build via UriBuilder properties so spaces/# in the path are correctly
		// percent-encoded (UriBuilder.Path's setter handles the escaping).
		var iosBuilder = new System.UriBuilder
		{
			Scheme = "shareddocuments",
			Host = string.Empty,
			Path = fullPath,
		};
		return Launcher.Default.OpenAsync(iosBuilder.Uri);
#else
		// Mac Catalyst → Finder, Windows → Explorer, Android → ACTION_VIEW (best-effort
		// — Android may not have a registered handler; Launcher returns false in that case).
		// new Uri(string) with an absolute filesystem path infers file:// and handles
		// percent-encoding + cross-platform separator quirks (Windows "\\", paths with
		// spaces, "#", "%", etc.) — the previous "file://" + fullPath concat threw
		// UriFormatException on those cases.
		var uri = new Uri(fullPath);
		return Launcher.Default.OpenAsync(uri);
#endif
	}
}
