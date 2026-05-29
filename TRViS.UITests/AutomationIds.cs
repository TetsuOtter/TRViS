namespace TRViS.UITests;

/// <summary>
/// AutomationId constants matching the values set in XAML files.
/// </summary>
public static class AutomationIds
{
	public static class Shell
	{
		public const string VersionLabel = "Shell.VersionLabel";

		public static class Flyout
		{
			public const string StartHome = "Shell.Flyout.StartHome";
			public const string DTAC = "Shell.Flyout.DTAC";
			public const string OriginalTimetableV1 = "Shell.Flyout.OriginalTimetableV1";
			public const string OriginalTimetableV2 = "Shell.Flyout.OriginalTimetableV2";
			public const string OriginalTimetableV4 = "Shell.Flyout.OriginalTimetableV4";
			public const string OriginalTimetableV6 = "Shell.Flyout.OriginalTimetableV6";
			public const string OriginalTimetableSimple = "Shell.Flyout.OriginalTimetableSimple";
			public const string Settings = "Shell.Flyout.Settings";
		}
	}

	/// <summary>
	/// Start/Home page (replaces the legacy SelectTrain page). The same page
	/// renders two visual states; the Start-mode buttons (Connect/SelectFile/Demo,
	/// Privacy/TPL links) live in StartBody, the Home-mode list/buttons live in
	/// HomeBody. Both share the animated app-header at the top.
	/// </summary>
	public static class StartHome
	{
		public const string AppHeader = "StartHome.AppHeader";
		public const string AppIcon = "StartHome.AppIcon";
		public const string Title = "StartHome.Title";
		public const string StartBody = "StartHome.StartBody";
		public const string HomeBody = "StartHome.HomeBody";

		// Start mode — primary buttons
		public const string ConnectServerButton = "StartHome.ConnectServerButton";
		public const string SelectFileButton = "StartHome.SelectFileButton";
		public const string LoadDemoButton = "StartHome.LoadDemoButton";

		// Start mode — privacy banner + footer links
		public const string PrivacyReconfirmBanner = "StartHome.PrivacyReconfirmBanner";
		public const string PrivacyReconfirmText = "StartHome.PrivacyReconfirmText";
		public const string PrivacyPolicyButton = "StartHome.PrivacyPolicyButton";
		public const string ThirdPartyLicensesButton = "StartHome.ThirdPartyLicensesButton";

		// Home mode — selection lists and action buttons
		public const string LoaderInfoTitle = "StartHome.LoaderInfoTitle";
		public const string LoaderInfoDetail = "StartHome.LoaderInfoDetail";
		// Two-step picker: each step has a list (full picker) and a chip (compact summary
		// shown after selection). Tapping the chip clears the selection and re-opens
		// the list. Auto-fill happens when a list arrives with exactly one item AND
		// the user has not previously cleared their selection.
		public const string WorkGroupList = "StartHome.WorkGroupList";
		public const string WorkGroupChip = "StartHome.WorkGroupChip";
		public const string WorkList = "StartHome.WorkList";
		public const string WorkChip = "StartHome.WorkChip";
		public const string OpenButton = "StartHome.OpenButton";
		public const string DisconnectButton = "StartHome.DisconnectButton";
		// Shown in the LoaderInfoCard only when a WebSocket loader's connection
		// dropped (#261). Tapping it re-runs the last WebSocket connect.
		public const string ReconnectButton = "StartHome.ReconnectButton";

		// UI_TEST-only seed seams.
		public const string TestSeedButton = "StartHome.TestSeedButton";
		public const string TestSeedGpsButton = "StartHome.TestSeedGpsButton";
		// Picks the first WorkGroup + first Work and commits via the same code path
		// as 開く. Lets DTAC-focused tests skip the picker UI without depending on
		// auto-cascade behavior in TimetableSelectionManager.
		public const string TestAutoOpenButton = "StartHome.TestAutoOpenButton";
		// Clears AppViewModel.ExternalResourceUrlHistory in-memory + on disk so
		// "empty history" tests can guarantee an empty list regardless of prior
		// noReset:true session state.
		public const string TestClearHistoryButton = "StartHome.TestClearHistoryButton";
		// Picks the first WorkGroup + first Work, replaces the Work record with a
		// clone that has HasETrainTimetable=true + a tiny PNG payload, and
		// navigates to DTAC. Lets the horizontal-timetable tests exercise the
		// "button visible" path without doctoring the sample data fixture.
		public const string TestSeedHorizontalTimetableButton = "StartHome.TestSeedHorizontalTimetableButton";
		// Seeds a minimal SQLite fixture into TimetableFileDirectory via the same
		// sqlite-net write path LoaderSQL reads from. Tests use this to verify the
		// MAUI runtime can actually open SQLite (catches Batteries_V2.Init /
		// linker stripping regressions that NUnit-only tests can't reach).
		public const string TestSeedSqliteButton = "StartHome.TestSeedSqliteButton";
		// Wipes TimetableFileDirectory contents so SelectFile tests start from a
		// known empty state regardless of prior session leftovers.
		public const string TestClearTimetablesButton = "StartHome.TestClearTimetablesButton";
		// Writes the canonical SelectFileDialog fixture (root file + sub-folder
		// with another file) into TimetableFileDirectory.
		public const string TestSeedSampleFilesButton = "StartHome.TestSeedSampleFilesButton";
		// Wipes TimetableFileDirectory + nulls FilePickerProvider.OverrideForTesting.
		// Tests call this in SetUp because iOS noReset:true keeps the documents
		// folder warm across sessions and the override static survives Driver.Quit().
		public const string TestClearSampleFilesButton = "StartHome.TestClearSampleFilesButton";
		// Writes a JSON fixture into CacheDirectory and installs a FilePicker
		// override that returns its path. Lets the Browse-fallback test exercise
		// the post-pick load path without driving the OS file picker UI.
		public const string TestSetupBrowseFallbackButton = "StartHome.TestSetupBrowseFallbackButton";
		// Cascades selection to a sample-data train whose NextTrainId is non-empty
		// (linear-train-1) so the NextTrainButton-visibility regression test for
		// #225 doesn't rely on the default first-train selection (which has an
		// empty NextTrainId).
		public const string TestSeedNextTrainSelectionButton = "StartHome.TestSeedNextTrainSelectionButton";
		// Sets AppViewModel.Loader=null + disposes the previous loader, mirroring
		// OnDisconnectClicked's clear path but skipping the user-facing confirm
		// dialog. Used by fixtures that share a single Appium session and need
		// each test to start from "Start mode" (LoadDemoButton visible) — by
		// default a prior LoadSample leaves the page in Home mode and the
		// LoadDemo button hidden behind the loader-info card.
		public const string TestClearLoaderButton = "StartHome.TestClearLoaderButton";
		// Sets a non-connected WebSocketNetworkSyncService as AppViewModel.Loader
		// and flips IsServerConnectionLost=true, putting Home into the #261
		// "サーバー未接続 + 再接続" state WITHOUT a real WebSocket server.
		public const string TestSimulateWebSocketDisconnectButton = "StartHome.TestSimulateWebSocketDisconnectButton";
		// Switches the UI language to English through the same ViewModel path
		// the Settings language picker uses (#40). Lets the i18n E2E assert a
		// {loc:Translate}-bound label flips without driving a native Picker.
		public const string TestSetLanguageEnglishButton = "StartHome.TestSetLanguageEnglishButton";
		// Pins the UI language to Japanese so fixtures asserting hard-coded
		// Japanese strings stay deterministic regardless of CI device locale.
		public const string TestSetLanguageJapaneseButton = "StartHome.TestSetLanguageJapaneseButton";
		// Builds a WebSocket-TYPED loader carrying real sample data, commits the
		// first WG/Work and navigates to DTAC. Lands on DTAC with the AppBar
		// status indicator in the Connected state (#266) so the indicator's
		// states can be E2E-verified without a real WebSocket server.
		public const string TestSimulateWebSocketConnectedButton = "StartHome.TestSimulateWebSocketConnectedButton";
		// Navigates directly to OriginalTimetableSimplePage via GoToAsync, bypassing
		// the flyout. On Android the FlyoutItem is replaced with a MenuItem whose
		// AutomationId does not map to resource-id, so flyout-based navigation fails.
		public const string TestNavigateToOTSimpleButton = "StartHome.TestNavigateToOTSimpleButton";
		public const string TestNavigateToOTV1Button = "StartHome.TestNavigateToOTV1Button";
		public const string TestCommitFirstWorkButton = "StartHome.TestCommitFirstWorkButton";

		// Direct invoker for OnSelectFileClicked. Bypasses the styled
		// SelectFileButton because Appium UIAutomator2's ACTION_CLICK against
		// MAUI's PrimaryActionButton-styled Button silently fails to dispatch
		// Button.Clicked on Android in the shared-session run (CI run
		// 25734141479: seam buttons fire, SelectFileButton does not, both have
		// enabled=true / clickable=true / visible=true in the accessibility
		// tree). The seam handler routes to OnSelectFileClicked so the test
		// still exercises Navigation.PushModalAsync(SelectFileDialog).
		public const string TestOpenSelectFileDialogButton = "StartHome.TestOpenSelectFileDialogButton";

		// Screenshot-regression determinism seams. TestFreezeClockButton pins
		// AppTimeProvider at 09:41:00 (Apple marketing time) so the DTAC
		// AppBar's live HH:mm:ss clock is pixel-stable across baseline
		// captures. The two theme buttons force app-wide Light / Dark so a
		// single shared Appium session can capture both palettes without
		// depending on the simulator's system appearance.
		public const string TestFreezeClockButton = "StartHome.TestFreezeClockButton";
		public const string TestForceLightThemeButton = "StartHome.TestForceLightThemeButton";
		public const string TestForceDarkThemeButton = "StartHome.TestForceDarkThemeButton";
		// Inverse of the two above. The screenshot fixture runs at Order(3),
		// so a frozen clock / forced theme would otherwise leak into the
		// dozens of later fixtures sharing the assembly-wide iOS session.
		public const string TestUnfreezeClockButton = "StartHome.TestUnfreezeClockButton";
		public const string TestResetThemeButton = "StartHome.TestResetThemeButton";
	}

	public static class PrivacyDialog
	{
		public const string Title = "PrivacyDialog.Title";
		public const string CloseButton = "PrivacyDialog.CloseButton";
		public const string AnalyticsSwitch = "PrivacyDialog.AnalyticsSwitch";
		public const string ResetButton = "PrivacyDialog.ResetButton";
		public const string SaveButton = "PrivacyDialog.SaveButton";
	}

	public static class DTAC
	{
		public const string MenuButton = "DTAC.MenuButton";
		public const string TimeLabel = "DTAC.TimeLabel";
		public const string TitleLabel = "DTAC.TitleLabel";
		public const string TabHako = "DTAC.TabHako";
		public const string TabTimetable = "DTAC.TabTimetable";
		public const string TabWorkAffix = "DTAC.TabWorkAffix";

		public const string StartEndRunButton = "DTAC.StartEndRunButton";
		public const string LocationServiceButton = "DTAC.LocationServiceButton";
		public const string OpenCloseButton = "DTAC.OpenCloseButton";

		// Per-train ハコ row button id (UI_TEST builds only). Append the TrainNumber.
		public const string HakoRowPrefix = "DTAC.HakoRow.";
		// Remarks panel toggle button (UI_TEST builds only), distinct from OpenCloseButton
		// which is the PageHeader's TrainInfo/BeforeDeparture toggle.
		public const string RemarksOpenCloseButton = "DTAC.RemarksOpenCloseButton";
		public const string TimetableScrollView = "DTAC.TimetableScrollView";
		public const string VerticalTimetableView = "DTAC.VerticalTimetableView";
		public const string NextTrainButton = "DTAC.NextTrainButton";

		// Visible only when the selected Work carries an embedded horizontal
		// timetable (HasETrainTimetable + ETrainTimetableContent). Tapping it
		// pushes HorizontalTimetablePage onto the Shell stack.
		public const string HorizontalTimetableButton = "DTAC.HorizontalTimetableButton";

		// UI_TEST-only seam: issues Shell.Current.GoToAsync("//StartHomePage")
		// directly so shared-session fixtures can recover from DTAC without
		// going through the flyout. The flyout is unreliable on Android when
		// VerticalView mode has locked orientation to Landscape — the MenuButton
		// click dispatches but the NavigationView never attaches to the
		// DrawerLayout, so WaitForFlyoutItem times out. Bypassing via direct
		// shell navigation triggers ViewHost.OnDisappearing which also unlocks
		// the orientation.
		public const string TestNavigateHomeButton = "DTAC.TestNavigateHomeButton";

		// UI_TEST-only state mirrors. The real AppBar TitleLabel / TimeLabel
		// don't reliably surface on iOS (iOS only exposes a Label in the
		// accessibility tree when its text is non-empty, and TimeLabel
		// additionally hides on narrow phones via a width threshold). These
		// invisible mirror labels are kept always non-empty by sentinel
		// prefixes (TestSeamTitlePrefix / TestSeamTimePrefix) so they are
		// always findable. Tests strip the prefix before asserting.
		public const string TestTitleSeam = "DTAC.TestTitleSeam";
		public const string TestTimeSeam = "DTAC.TestTimeSeam";
		public const string TestSeamTitlePrefix = "T:";
		public const string TestSeamTimePrefix = "C:";

		// UI_TEST-only seam: changes the first non-InfoRow in the current train's
		// TimetableRows to IsInfoRow=true, then re-sets AppViewModel.SelectedTrainData
		// with the modified clone. This exercises the WebSocket soft-update code path
		// (same train ID → ApplyPositionAlignedDiff → PropertyChanged("IsInfoRow") →
		// UpdateAllComponents) to reproduce the "station-name label stays visible after
		// IsInfoRow false→true transition" bug.
		public const string TestSeedIsInfoRowTransitionButton = "DTAC.TestSeedIsInfoRowTransitionButton";

		// AutomationId pattern for timetable row components (set in UI_TEST builds only).
		// Use string.Format: TimetableRowStationNamePattern.Replace("{0}", rowIndex.ToString())
		public const string TimetableRowStationNamePattern = "TimetableRow.{0}.StationName";
		public const string TimetableRowInfoRowPattern = "TimetableRow.{0}.InfoRow";

		// UI_TEST-only seams (#266): mutate the singleton AppViewModel's
		// WebSocket connection flags so the AppBar status indicator can be
		// driven through Connected/Disconnected/Reconnecting while on DTAC
		// (the only page that shows the AppBar) without a real server.
		public const string TestWsConnectedButton = "DTAC.TestWsConnectedButton";
		public const string TestWsDisconnectedButton = "DTAC.TestWsDisconnectedButton";
		public const string TestWsReconnectingButton = "DTAC.TestWsReconnectingButton";
	}

	/// <summary>
	/// Shared title bar (TRViS.DTAC.AppBar), shown on the DTAC ViewHost and the
	/// HorizontalTimetable page.
	/// </summary>
	public static class AppBar
	{
		// UI_TEST-only invisible mirror Label reflecting AppViewModel.
		// ServerConnectionStatus (#266). Ellipse/ActivityIndicator don't
		// reliably surface in the iOS accessibility tree, so the indicator's
		// state is mirrored here as ConnectionStatusPrefix + enum name (always
		// non-empty → always findable). Strip the prefix before asserting.
		public const string ConnectionStatus = "AppBar.ConnectionStatus";
		public const string ConnectionStatusPrefix = "S:";

		// The real (visible) status indicator. Tappable when Disconnected to
		// confirm reconnect (#266). Not UI_TEST-gated — it's a real
		// interactive control.
		public const string ConnectionStatusButton = "AppBar.ConnectionStatusButton";
	}

	/// <summary>
	/// Horizontal timetable page (PNG/JPG/PDF/URI displayed in a WebView).
	/// Reached by tapping <see cref="DTAC.HorizontalTimetableButton"/>.
	/// </summary>
	public static class HorizontalTimetable
	{
		public const string WebView = "HorizontalTimetable.WebView";
		// AppBar back button. Tapping it pops the page back to DTAC so the
		// Shell flyout is reachable again. Exposed to the test layer so a
		// fixture-end TearDown can return the app to a Shell-rooted page
		// (HT is a Shell.GoToAsync push and the flyout is gated to roots).
		public const string BackButton = "HorizontalTimetable.BackButton";
	}

	/// <summary>
	/// Connect-to-Server modal dialog (replaces the legacy SelectOnlineResource popup).
	/// Two states: history list (rich cards keyed by URL) and a new-connection form
	/// (URL Entry + "save connection" Switch + Connect button).
	/// </summary>
	public static class ConnectServer
	{
		public const string Title = "ConnectServer.Title";
		public const string CloseButton = "ConnectServer.CloseButton";

		// History list state
		public const string HistoryList = "ConnectServer.HistoryList";
		// Per-row id is "<HistoryItemPrefix><url>" — entire card is tappable
		// (no shared Entry, so tap loads directly).
		public const string HistoryItemPrefix = "ConnectServer.HistoryItem.";
		public const string NewConnectionButton = "ConnectServer.NewConnectionButton";

		// New-connection form state
		public const string BackToHistoryButton = "ConnectServer.BackToHistoryButton";
		public const string UrlInput = "ConnectServer.UrlInput";
		public const string SaveConnectionSwitch = "ConnectServer.SaveConnectionSwitch";
		public const string ConnectButton = "ConnectServer.ConnectButton";
	}

	/// <summary>
	/// Select-File modal dialog (replaces the direct OS FilePicker behaviour).
	/// Lists JSON/SQLite files from the app documents folder as rich cards plus
	/// a "他の場所からファイルを開く" button that falls back to the OS picker.
	/// </summary>
	public static class SelectFile
	{
		public const string Title = "SelectFile.Title";
		public const string CloseButton = "SelectFile.CloseButton";

		// File list state
		public const string FileList = "SelectFile.FileList";
		// Label inside FileListView. Used by IsFileListVisible() because Android's
		// UiAutomator2 doesn't surface a ScrollView's AutomationId reliably, so
		// probing FileList directly returns false even when files are showing.
		// Labels are surfaced consistently across all four platforms.
		public const string FileListHint = "SelectFile.FileListHint";
		// Per-row ids — entire card is tappable. Folder cards drill into the folder;
		// the up-folder card is rendered above sibling cards when not at root.
		public const string FileItemPrefix = "SelectFile.FileItem.";
		public const string FolderItemPrefix = "SelectFile.FolderItem.";
		public const string UpFolderItem = "SelectFile.UpFolderItem";

		// Breadcrumb showing the current relative path (only visible when not at root).
		public const string Breadcrumb = "SelectFile.Breadcrumb";

		// Empty state (visible when the current folder has no folders or supported files).
		public const string EmptyMessage = "SelectFile.EmptyMessage";

		// Always-visible footer actions.
		// Browse: opens the OS FilePicker for files outside the documents folder.
		// OpenStorageLocation: reveals the documents folder in the OS file manager.
		public const string BrowseButton = "SelectFile.BrowseButton";
		public const string OpenStorageLocationButton = "SelectFile.OpenStorageLocationButton";
	}

	public static class Settings
	{
		public const string ReloadSavedButton = "Settings.ReloadSavedButton";
		public const string SaveButton = "Settings.SaveButton";
	}

	public static class OriginalTimetable
	{
		public static class Simple
		{
			public const string Root = "OriginalTimetable.Simple.Root";
			public const string PageLabel = "OriginalTimetable.Simple.PageLabel";
		}

		public static class V1
		{
			public const string Root = "OriginalTimetable.V1.Root";
			public const string TabletGrid = "OriginalTimetable.V1.TabletGrid";
			public const string CompactRoot = "OriginalTimetable.V1.Compact.Root";
			public const string CompactHeader = "OriginalTimetable.V1.Compact.Header";
			public const string CompactHeaderTrainNumber = "OriginalTimetable.V1.Compact.Header.TrainNumber";
			public const string CompactEmptyState = "OriginalTimetable.V1.Compact.EmptyState";
			public const string CompactRowsList = "OriginalTimetable.V1.Compact.RowsList";
			public const string Header = "OriginalTimetable.V1.Header";
			public const string HeaderTypeChip = "OriginalTimetable.V1.Header.TypeChip";
			public const string TabletFlyoutToggle = "OriginalTimetable.V1.Tablet.FlyoutToggle";
			public const string CompactFlyoutToggle = "OriginalTimetable.V1.Compact.FlyoutToggle";
			public const string HeaderTrainNumber = "OriginalTimetable.V1.Header.TrainNumber";
			public const string HeaderCarCount = "OriginalTimetable.V1.Header.CarCount";
			public const string HeaderMaxSpeed = "OriginalTimetable.V1.Header.MaxSpeed";
			public const string EmptyState = "OriginalTimetable.V1.EmptyState";
			public const string RowsList = "OriginalTimetable.V1.RowsList";
			public const string RowPrefix = "OriginalTimetable.V1.Row.";
			public static string RowFor(string rowId) => RowPrefix + rowId;
			public static string MarkerFor(string rowId) => RowPrefix + rowId + ".Marker";
			public static string MemoFor(string rowId) => RowPrefix + rowId + ".Memo";
			public static string ClearFor(string rowId) => RowPrefix + rowId + ".Clear";
			public static string MarkerBadgeFor(string rowId) => RowPrefix + rowId + ".MarkerBadge";
			public static string NoteBodyFor(string rowId) => RowPrefix + rowId + ".NoteBody";
			public static class MemoSheet
			{
				public const string Root = "OriginalTimetable.V1.MemoSheet";
				public const string Scrim = "OriginalTimetable.V1.MemoSheet.Scrim";
				public const string Editor = "OriginalTimetable.V1.MemoSheet.Editor";
				public const string Save = "OriginalTimetable.V1.MemoSheet.Save";
				public const string Delete = "OriginalTimetable.V1.MemoSheet.Delete";
				public const string Cancel = "OriginalTimetable.V1.MemoSheet.Cancel";
			}
			public const string TestCycleMarkerRow0Button = "OriginalTimetable.V1.Test.CycleMarkerRow0";
			public const string TestClearMarkerRow0Button = "OriginalTimetable.V1.Test.ClearMarkerRow0";
		}

		public static class MarkerPopover
		{
			public const string None = "OriginalTimetable.MarkerPopover.None";
			public const string Flag = "OriginalTimetable.MarkerPopover.Flag";
			public const string Caution = "OriginalTimetable.MarkerPopover.Caution";
			public const string Star = "OriginalTimetable.MarkerPopover.Star";
		}

		public static class V2
		{
			public const string Root = "OriginalTimetable.V2.Root";
			public const string TabletGrid = "OriginalTimetable.V2.TabletGrid";
			public const string CompactGrid = "OriginalTimetable.V2.CompactGrid";
			public const string Header = "OriginalTimetable.V2.Header";
			public const string EmptyState = "OriginalTimetable.V2.EmptyState";
			public const string RowsList = "OriginalTimetable.V2.RowsList";
			public const string CompactHeader = "OriginalTimetable.V2.Compact.Header";
			public const string CompactEmptyState = "OriginalTimetable.V2.Compact.EmptyState";
			public const string CompactRowsList = "OriginalTimetable.V2.Compact.RowsList";
			public const string RowPrefix = "OriginalTimetable.V2.Row.";
			public static string RowFor(string rowId) => RowPrefix + rowId;
			public static string MarkerFor(string rowId) => RowPrefix + rowId + ".Marker";
			public static string MemoFor(string rowId) => RowPrefix + rowId + ".Memo";
			public static string ClearFor(string rowId) => RowPrefix + rowId + ".Clear";
			public static string MarkerBadgeFor(string rowId) => RowPrefix + rowId + ".MarkerBadge";
			public static string NoteBodyFor(string rowId) => RowPrefix + rowId + ".NoteBody";
		}

		public static class V4
		{
			public const string Root = "OriginalTimetable.V4.Root";
			public const string TabletGrid = "OriginalTimetable.V4.TabletGrid";
			public const string CompactGrid = "OriginalTimetable.V4.CompactGrid";
			public const string TrainStripe = "OriginalTimetable.V4.TrainStripe";
			public const string TrainStripeTrainNumber = "OriginalTimetable.V4.TrainStripe.TrainNumber";
			public const string Hero = "OriginalTimetable.V4.Hero";
			public const string HeroMarkerBadge = "OriginalTimetable.V4.Hero.MarkerBadge";
			public const string HeroStation = "OriginalTimetable.V4.HeroStation";
			public const string EmptyState = "OriginalTimetable.V4.EmptyState";
			public const string CompactEmptyState = "OriginalTimetable.V4.Compact.EmptyState";
			public const string MiniList = "OriginalTimetable.V4.MiniList";
			public const string CompactMiniList = "OriginalTimetable.V4.Compact.MiniList";
			public const string RowPrefix = "OriginalTimetable.V4.Row.";
			public static string RowFor(string rowId) => RowPrefix + rowId;
			public static string MarkerFor(string rowId) => RowPrefix + rowId + ".Marker";
			public static string MarkerBadgeFor(string rowId) => RowPrefix + rowId + ".MarkerBadge";
		}

		public static class V6
		{
			public const string Root = "OriginalTimetable.V6.Root";
			public const string TabletGrid = "OriginalTimetable.V6.TabletGrid";
			public const string CompactGrid = "OriginalTimetable.V6.CompactGrid";
			public const string Masthead = "OriginalTimetable.V6.Masthead";
			public const string CurrentBlock = "OriginalTimetable.V6.CurrentBlock";
			public const string CurrentBlockMarkerBadge = "OriginalTimetable.V6.CurrentBlock.MarkerBadge";
			public const string CurrentBlockStationName = "OriginalTimetable.V6.CurrentBlock.StationName";
			public const string EmptyState = "OriginalTimetable.V6.EmptyState";
			public const string UpcomingList = "OriginalTimetable.V6.UpcomingList";
			public const string CompactMasthead = "OriginalTimetable.V6.Compact.Masthead";
			public const string CompactCurrentBlock = "OriginalTimetable.V6.Compact.CurrentBlock";
			public const string CompactEmptyState = "OriginalTimetable.V6.Compact.EmptyState";
			public const string CompactUpcomingList = "OriginalTimetable.V6.Compact.UpcomingList";
			public const string RowPrefix = "OriginalTimetable.V6.Row.";
			public static string RowFor(string rowId) => RowPrefix + rowId;
			public static string MarkerFor(string rowId) => RowPrefix + rowId + ".Marker";
			public static string MarkerBadgeFor(string rowId) => RowPrefix + rowId + ".MarkerBadge";
		}
	}

	public static class ThirdParty
	{
		public const string LicenseList = "ThirdParty.LicenseList";
		// Visible only when the page is hosted as a modal (asModal:true).
		public const string ModalCloseButton = "ThirdParty.ModalCloseButton";
	}
}
