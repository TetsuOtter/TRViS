using System.Collections.ObjectModel;

using TRViS.DTAC;
using TRViS.IO;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.RootPages;

// Lightweight presenter records used by the WorkGroup / Work CollectionView
// templates. Subtitle aggregates whatever rich detail is available from the
// loader (Work count for groups; Train count + AffectDate for works) so the
// picker rows aren't just bare names.
public sealed record WorkGroupListItem(WorkGroup Source, string Name, string Subtitle);
public sealed record WorkListItem(Work Source, string Name, string Subtitle);

public partial class StartHomePage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(StartHomePage);
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	AppViewModel viewModel { get; }

	// Mode tracks whether the body shows Start (no Loader) or Home (Loader present).
	enum PageMode { Start, Home }
	PageMode _currentMode = PageMode.Start;

	// ----- Tentative (pre-Open) selection state -----
	// The Home page deliberately does NOT mirror its picker state into
	// AppViewModel.SelectedWorkGroup / SelectedWork. Those are the *committed*
	// selections used by DTAC; they only change when the user presses 開く.
	// This lets the user explore the picker without polluting DTAC, and gives
	// the 開く button real semantic weight ("commit my choice").
	WorkGroup? _pendingWorkGroup;
	Work? _pendingWork;
	readonly ObservableCollection<WorkGroupListItem> _workGroupItems = new();
	readonly ObservableCollection<WorkListItem> _workItems = new();
	// Per-loader caches for the subtitle counts. RebuildWorkGroupItems is called on
	// every WorkGroupList PropertyChanged (e.g. each websocket Refresh), and each
	// item's subtitle calls loader.GetWorkList(wg.Id).Count — a DB roundtrip on
	// LoaderSQL. With N WorkGroups and M Works per WG, an unguarded rebuild costs
	// O(N+N*M) DB reads per push. Cache by Id, scoped to the *current* loader so
	// a swap clears stale entries.
	ILoader? _countCacheLoader;
	readonly Dictionary<string, int> _workCountByGroupId = new();
	readonly Dictionary<string, int> _trainCountByWorkId = new();
	// Re-entrancy guard: we set CollectionView.SelectedItem programmatically when
	// restoring tentative state from the AppViewModel, and that fires SelectionChanged.
	// The flag prevents that synthetic change from being treated as a user pick.
	bool _suppressSelectionChanged;
	// Guard: 開く's commit sets SelectedWorkGroup -> the cascade auto-picks the first
	// Work of that group BEFORE we then set SelectedWork to the user's pending pick.
	// Without this guard, the intermediate SelectedWork PropertyChanged would yank
	// _pendingWork to the auto-picked one via SyncPendingFromCommitted, causing a
	// brief visible flicker.
	bool _committingOpen;

	// Animation tunables. Scale always stays 1.0 — icon size is set via HeightRequest.
	// HOME_COMPACT_ICON_SIZE: smaller icon used in Home mode on small screens (height ≤ HOME_SMALL_HEIGHT_THRESHOLD).
	const uint TRANSITION_MS = 380;
	// Below this short-side dimension we assume "smartphone" form factor. Above,
	// "tablet" — even in landscape orientation iPad mini's short side is well
	// over 700, so this picks the right side reliably without per-device
	// hard-coding.
	const double PHONE_SHORT_SIDE_MAX = 500;

	// Compact-portrait threshold. When the page is portrait (or square) AND the
	// page height is below this value, AppIcon / Title / button heights / body
	// padding all shrink so the hero icon and primary buttons stop colliding in
	// row 1. Targets iPad Slide Over (~568h logical), iPad split-view in narrow
	// configurations, and Mac Catalyst windows the user has resized small. iPhone
	// portrait (≥844) stays at the full-size hero treatment.
	// We use slightly different enter/exit thresholds (hysteresis) so a window
	// dragged across the boundary on Mac Catalyst doesn't flicker styles every
	// pixel while crossing — enter compact at <800, exit at ≥820.
	const double COMPACT_HEIGHT_ENTER = 800;
	const double COMPACT_HEIGHT_EXIT = 820;

	// Landscape-phone compact threshold. iPhone SE class devices (1st gen
	// 320h, 2nd/3rd gen 375h) cannot fit the natural 80px primary buttons
	// (which wrap into two rows because the right column is also too narrow
	// to hold two 220-basis buttons side-by-side) plus the 44px demo button
	// inside the right-column body even in landscape. Below this short-side
	// height we apply the same body/button compaction used in narrow
	// portrait. iPhone 13/14 standard (≥390h landscape) stays at full size.
	const double LANDSCAPE_COMPACT_HEIGHT_ENTER = 380;
	const double LANDSCAPE_COMPACT_HEIGHT_EXIT = 400;

	// Below this page height, Home-mode header is further compacted (icon shrunk,
	// title hidden) so the WorkGroup/Work list has more vertical room.
	const double HOME_SMALL_HEIGHT_THRESHOLD = 900.0;
	const double HOME_COMPACT_ICON_SIZE = 80.0;

	// ----- Fixed row heights (base values, scaled by system font scale below) -----
	// Rows that contain no real content of their own in the layered structure are
	// hardcoded so they don't depend on size-mirror placeholders. The body grids
	// (StartGrid / HomeGrid) reserve the same row sizes via the shared static
	// RowDefinitionCollections, guaranteeing alignment with BackgroundGrid.
	const double HOME_HEADER_ROW_HEIGHT_BASE = 128.0;
	const double FOOTER_ROW_HEIGHT_BASE = 36.0;
	const double LOADER_INFO_ROW_HEIGHT_BASE = 60.0;
	const double HOME_BUTTONS_ROW_HEIGHT_BASE = 44.0;
	const double START_BODY_ROW_HEIGHT_FULL_BASE = 280.0;
	const double START_BODY_ROW_HEIGHT_COMPACT_BASE = 200.0;

	// System text-scale factor read at type load (iOS Dynamic Type / Android
	// Configuration.FontScale / Windows UISettings.TextScaleFactor). Applied to
	// the fixed row heights so accessibility text-size settings don't clip rows.
	static readonly double _fontScale = ReadSystemFontScale();

	static readonly double HOME_HEADER_ROW_HEIGHT = HOME_HEADER_ROW_HEIGHT_BASE * _fontScale;
	static readonly double FOOTER_ROW_HEIGHT = FOOTER_ROW_HEIGHT_BASE * _fontScale;
	static readonly double LOADER_INFO_ROW_HEIGHT = LOADER_INFO_ROW_HEIGHT_BASE * _fontScale;
	static readonly double HOME_BUTTONS_ROW_HEIGHT = HOME_BUTTONS_ROW_HEIGHT_BASE * _fontScale;
	static readonly double START_BODY_ROW_HEIGHT_FULL = START_BODY_ROW_HEIGHT_FULL_BASE * _fontScale;
	static readonly double START_BODY_ROW_HEIGHT_COMPACT = START_BODY_ROW_HEIGHT_COMPACT_BASE * _fontScale;

	static double ReadSystemFontScale()
	{
#if ANDROID
		try { return Android.App.Application.Context?.Resources?.Configuration?.FontScale ?? 1.0; }
		catch { return 1.0; }
#elif IOS || MACCATALYST
		try
		{
			var cat = UIKit.UIApplication.SharedApplication?.PreferredContentSizeCategory;
			return cat?.ToString() switch
			{
				"UICTContentSizeCategoryXS" => 0.82,
				"UICTContentSizeCategoryS" => 0.88,
				"UICTContentSizeCategoryM" => 0.94,
				"UICTContentSizeCategoryL" => 1.0,
				"UICTContentSizeCategoryXL" => 1.12,
				"UICTContentSizeCategoryXXL" => 1.24,
				"UICTContentSizeCategoryXXXL" => 1.35,
				"UICTContentSizeCategoryAccessibilityM" => 1.65,
				"UICTContentSizeCategoryAccessibilityL" => 1.94,
				"UICTContentSizeCategoryAccessibilityXL" => 2.35,
				"UICTContentSizeCategoryAccessibilityXXL" => 2.76,
				"UICTContentSizeCategoryAccessibilityXXXL" => 3.12,
				_ => 1.0,
			};
		}
		catch { return 1.0; }
#elif WINDOWS
		try { return new Windows.UI.ViewManagement.UISettings().TextScaleFactor; }
		catch { return 1.0; }
#else
		return 1.0;
#endif
	}

	// Tracks whether we're currently laid out for phone-landscape (header on the
	// left, body on the right) or the default vertical layout. Updated from
	// ApplyOrientationLayout; consumed by ComputeStartHeaderTranslationY (which
	// returns 0 in landscape because the header has no vertical "centering"
	// translation to apply when it owns its own column) and ApplyHeaderLayoutInstant.
	bool _isLandscapePhone;

	// Tracks whether the compact-portrait styling (smaller icon / shorter
	// buttons / tighter padding) is currently applied. Updated from
	// ApplyHeightCompactStyling — the bool guard keeps the per-property writes
	// idempotent across SizeChanged events that don't actually cross the
	// threshold.
	bool _isCompactHeight;
	bool _compactHeightApplied;

	double ComputeStartHeaderTranslationY() => 0;

	public StartHomePage()
	{
		logger.Trace("Creating");

		InitializeComponent();

		// Assign the canonical row schemes up-front so the initial render uses the
		// same RowDefinitionCollection instances as later mode swaps. Without this,
		// the XAML-parsed defaults would be replaced on the first SizeChanged pass,
		// leaving a transient mismatch between Background and the body grids.
		UpdateGridRowDefinitions(_currentMode);

		viewModel = InstanceManager.AppViewModel;
		BindingContext = viewModel;

		WorkGroupListView.ItemsSource = _workGroupItems;
		WorkListView.ItemsSource = _workItems;

		// Apply initial header layout once we have a measured size.
		SizeChanged += OnSizeChangedFirstLayout;
		AppHeader.SizeChanged += (_, __) =>
		{
			// AppHeader.Height is read by ComputeStartHeaderTranslationY; recompute
			// once it lands so the header centers correctly even on the first frame.
			RecomputeStartModeHeaderPosition();
		};
		// StartBody.Height feeds ComputeStartHeaderTranslationY too — without this
		// handler, the first SizeChanged on the page fires before StartBody is
		// measured, the formula falls back to bodyHeight=0, and translation ends
		// up large enough to push the header (and the AppTitle inside it) into
		// StartBody's row, where Z-order hides it on small screens.
		StartBody.SizeChanged += (_, __) => RecomputeStartModeHeaderPosition();
		// HomeBody.Height drives UpdateListHeights — re-run whenever HomeBody
		// changes size (orientation change, window resize, spacer update).
		HomeBody.SizeChanged += (_, __) => UpdateListHeights();

#if UI_TEST
		AddTestOpenSelectFileDialogSeam();
#endif

		logger.Trace("Created");
	}

	void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		// WebSocketNetworkSyncService raises TimetableUpdated synchronously on
		// the WS receive task; that cascades into AppViewModel/SelectionManager
		// PropertyChanged here, where we mutate CollectionView.SelectedItem,
		// IsVisible, animations, etc. All of that must happen on the UI thread —
		// hop now so off-thread WS callbacks don't trigger MAUI dispatcher
		// assertions or visual glitches.
		if (MainThread.IsMainThread)
		{
			HandleViewModelPropertyChanged(e.PropertyName);
		}
		else
		{
			string? propertyName = e.PropertyName;
			MainThread.BeginInvokeOnMainThread(() => HandleViewModelPropertyChanged(propertyName));
		}
	}

	void HandleViewModelPropertyChanged(string? propertyName)
	{
		switch (propertyName)
		{
			case nameof(AppViewModel.Loader):
				logger.Debug("Loader changed -> evaluate page mode");
				// New loader -> wipe tentative state and rebuild the WorkGroup picker.
				ResetPendingSelection();
				RebuildWorkGroupItems();
				_ = ApplyModeForCurrentLoaderAsync();
				break;

			case nameof(AppViewModel.LoaderSourceLabel):
				UpdateLoaderInfoLabels();
				break;

			case nameof(AppViewModel.WorkGroupList):
				// WorkGroupList changes can come from a Refresh() (websocket) which
				// may also reset committed AppViewModel selections. Rebuild the items;
				// only sync tentative from committed when committed actually has a
				// value — otherwise a websocket Refresh would silently clobber the
				// user's mid-pick on Home (committed is null -> _pendingWorkGroup
				// would be forced to null).
				// A Refresh may also have added/removed Works/Trains under the
				// same loader instance, so drop count caches to force fresh reads.
				InvalidateCountCaches();
				RebuildWorkGroupItems();
				if (viewModel.SelectedWorkGroup is not null || viewModel.SelectedWork is not null)
					SyncPendingFromCommitted();
				else
					SyncListViewSelections();
				RefreshStepUi();
				break;

			case nameof(AppViewModel.SelectedWorkGroup):
			case nameof(AppViewModel.SelectedWork):
				// Committed selection moved underneath us (e.g. websocket Refresh chose
				// a different fallback). Re-sync the tentative state so the user sees
				// what's actually committed when they return to this page.
				// Skip during 開く's own commit, where the cascade between SetSelectedWorkGroup
				// and SetSelectedWork would otherwise overwrite the user's pending pick.
				if (!_committingOpen)
				{
					SyncPendingFromCommitted();
					RefreshStepUi();
				}
				break;
		}
	}

	void OnSizeChangedFirstLayout(object? sender, EventArgs e)
	{
		if (Width <= 0 || Height <= 0)
			return;

		// Apply orientation first so RowDefinitions / ColumnDefinitions are settled
		// before we recompute the header translation (which depends on them).
		ApplyOrientationLayout();

		// Compact-portrait styling tweaks (icon / button heights / padding) run
		// after the orientation is decided so they can short-circuit on
		// landscape-phone, where we already use a separate horizontal layout.
		ApplyHeightCompactStyling();

		// Home-mode compact header may override the Start-mode sizing above.
		ApplyHomeModeCompactHeader();

		// Apply initial state without animation. We do this on every size change in case the
		// window resizes (desktop) or device rotates — but only animate when the *mode* changes;
		// pure size changes get a snap-update.
		ApplyHeaderLayoutInstant(_currentMode);
	}

	void RecomputeStartModeHeaderPosition()
	{
		if (_currentMode != PageMode.Start || Width <= 0 || Height <= 0)
			return;
		AppHeader.TranslationY = ComputeStartHeaderTranslationY();
	}

	/// <summary>
	/// Switches between vertical (header above body) and horizontal (header
	/// left, body right) layouts. The horizontal layout kicks in only on phones
	/// in landscape — where the vertical layout would otherwise overlap the
	/// AppHeader, the privacy banner, and StartBody's buttons all on top of
	/// each other (the page height is just too small to stack them).
	/// </summary>
	void ApplyOrientationLayout()
	{
		if (Width <= 0 || Height <= 0)
			return;
		bool isLandscapePhone = Width > Height && Math.Min(Width, Height) < PHONE_SHORT_SIDE_MAX;
		// Idempotent: skip the reshuffle when the orientation hasn't changed
		// after the initial layout has been applied.
		if (isLandscapePhone == _isLandscapePhone && _orientationLayoutApplied)
			return;
		_isLandscapePhone = isLandscapePhone;
		_orientationLayoutApplied = true;
		// Each element lives in one of BackgroundGrid / StartGrid / HomeGrid; the
		// attached Grid.Row/Column properties are resolved against that parent. The
		// XAML declares the portrait layout as defaults — only landscape phone
		// (header left column, body right column) overrides them here.
		if (isLandscapePhone)
		{
			// AppHeader → Background left column, spanning the Star area only
			// (rows 0-2) so Row 3 stays free for LoaderInfoCard underneath. The
			// icon + "TRViS" title center vertically inside the Star area.
			Grid.SetRow(AppHeader, 0); Grid.SetRowSpan(AppHeader, 3);
			Grid.SetColumnSpan(AppHeader, 1);
			// LoaderInfoCard → Home grid left column row 3 (directly above the
			// privacy/TPL footer). Sits beside HomeButtonsRow in the right column.
			Grid.SetRow(LoaderInfoCard, 3);
			Grid.SetColumn(LoaderInfoCard, 0);
			Grid.SetColumnSpan(LoaderInfoCard, 1);
			LoaderInfoCard.VerticalOptions = LayoutOptions.Center;
			// StartBody → Start grid right column, spanning the Star area.
			Grid.SetRow(StartBody, 0); Grid.SetRowSpan(StartBody, 3);
			Grid.SetColumn(StartBody, 1); Grid.SetColumnSpan(StartBody, 1);
			StartBody.VerticalOptions = LayoutOptions.Center;
			// HomeBody → Home grid right column, just the Star row.
			Grid.SetRow(HomeBody, 2); Grid.SetRowSpan(HomeBody, 1);
			Grid.SetColumn(HomeBody, 1); Grid.SetColumnSpan(HomeBody, 1);
			// HomeButtonsRow → Home grid right column row 3, beside LoaderInfoCard.
			Grid.SetColumn(HomeButtonsRow, 1); Grid.SetColumnSpan(HomeButtonsRow, 1);
		}
		else
		{
			// Portrait/tablet: span both columns.
			Grid.SetRow(AppHeader, 0); Grid.SetRowSpan(AppHeader, 1);
			Grid.SetColumnSpan(AppHeader, 2);
			Grid.SetRow(LoaderInfoCard, 1);
			Grid.SetColumn(LoaderInfoCard, 0);
			Grid.SetColumnSpan(LoaderInfoCard, 2);
			LoaderInfoCard.VerticalOptions = LayoutOptions.Fill;
			Grid.SetRow(StartBody, 2); Grid.SetRowSpan(StartBody, 1);
			Grid.SetColumn(StartBody, 0); Grid.SetColumnSpan(StartBody, 2);
			StartBody.VerticalOptions = LayoutOptions.End;
			Grid.SetRow(HomeBody, 2); Grid.SetRowSpan(HomeBody, 1);
			Grid.SetColumn(HomeBody, 0); Grid.SetColumnSpan(HomeBody, 2);
			Grid.SetColumn(HomeButtonsRow, 0); Grid.SetColumnSpan(HomeButtonsRow, 2);
		}
		UpdateGridRowDefinitions(_currentMode);
	}

	bool _orientationLayoutApplied;

	/// <summary>
	/// Tightens the AppHeader / StartBody styling for narrow-portrait windows
	/// (iPad Slide Over, multitasking split, manually resized Mac Catalyst).
	/// In those cases the natural 160px hero icon plus 80px primary buttons
	/// don't both fit in the available vertical space, so the Star row
	/// collapses and StartBody overlaps the icon. Shrinking the icon, button
	/// heights, and vertical padding restores breathing room without changing
	/// the standard portrait look on phones / full iPad.
	/// </summary>
	void ApplyHeightCompactStyling()
	{
		if (Width <= 0 || Height <= 0)
			return;
		// Compact in two situations:
		//   - portrait/square narrow windows (iPad Slide Over, split-view, small
		//     Mac Catalyst windows): hero icon + primary buttons would otherwise
		//     overlap in row 1.
		//   - landscape phone with very small short-side (iPhone SE class):
		//     even though the horizontal split layout owns the header column,
		//     the right-column body still has to stack the wrapped primary
		//     buttons + demo button, which don't fit at the natural 80/44 px.
		bool isPortrait = !_isLandscapePhone && Width <= Height;
		double enter = _isLandscapePhone ? LANDSCAPE_COMPACT_HEIGHT_ENTER : COMPACT_HEIGHT_ENTER;
		double exit = _isLandscapePhone ? LANDSCAPE_COMPACT_HEIGHT_EXIT : COMPACT_HEIGHT_EXIT;
		bool eligible = isPortrait || _isLandscapePhone;
		bool isCompact;
		if (!_compactHeightApplied)
		{
			// First measurement: pick the appropriate side of the band based
			// solely on the enter threshold, then both sticky branches below
			// will hold us there until we cross the opposite threshold.
			isCompact = eligible && Height < enter;
		}
		else if (_isCompactHeight)
		{
			// Currently compact: exit only when we comfortably clear EXIT.
			isCompact = eligible && Height < exit;
		}
		else
		{
			// Currently full-size: enter compact only below the lower threshold.
			isCompact = eligible && Height < enter;
		}
		if (isCompact == _isCompactHeight && _compactHeightApplied)
			return;
		_isCompactHeight = isCompact;
		_compactHeightApplied = true;

		if (isCompact)
		{
			// AppHeader sizing is owned by ApplyHomeModeCompactHeader's
			// landscape branch (left-column layout sets icon=128, title=36);
			// skip it here so the next ApplyHomeModeCompactHeader pass doesn't
			// just override us.
			if (!_isLandscapePhone)
			{
				AppIcon.HeightRequest = 96;
				AppIcon.WidthRequest = 96;
				AppTitle.FontSize = 32;
				AppHeader.Padding = new Thickness(16, 16, 16, 0);
				AppHeader.Spacing = 4;
			}
			ConnectServerButton.HeightRequest = 56;
			ConnectServerButton.FontSize = 17;
			SelectFileButton.HeightRequest = 56;
			SelectFileButton.FontSize = 17;
			LoadDemoButton.HeightRequest = 36;
			// Landscape body sits in the narrow right column — drop the
			// horizontal padding too so wrapped buttons get a touch more width.
			StartBody.Padding = _isLandscapePhone
				? new Thickness(12, 4, 12, 8)
				: new Thickness(24, 4, 24, 8);
			StartBody.RowSpacing = 4;
			PortraitStartRows[2].Height = new GridLength(START_BODY_ROW_HEIGHT_COMPACT);
			PortraitStartRowsBg[2].Height = new GridLength(START_BODY_ROW_HEIGHT_COMPACT);
		}
		else
		{
			if (!_isLandscapePhone)
			{
				AppIcon.HeightRequest = 160;
				AppIcon.WidthRequest = 160;
				AppTitle.FontSize = 44;
				AppHeader.Padding = new Thickness(16, 32, 16, 0);
				AppHeader.Spacing = 8;
			}
			ConnectServerButton.HeightRequest = 80;
			ConnectServerButton.FontSize = 20;
			SelectFileButton.HeightRequest = 80;
			SelectFileButton.FontSize = 20;
			LoadDemoButton.HeightRequest = 44;
			StartBody.Padding = new Thickness(24, 8, 24, 24);
			StartBody.RowSpacing = 8;
			PortraitStartRows[2].Height = new GridLength(START_BODY_ROW_HEIGHT_FULL);
			PortraitStartRowsBg[2].Height = new GridLength(START_BODY_ROW_HEIGHT_FULL);
		}
	}

	bool IsHomeModeCompact() =>
		_currentMode == PageMode.Home && !_isLandscapePhone && Height > 0 && Height <= HOME_SMALL_HEIGHT_THRESHOLD;

	/// <summary>
	/// When in Home mode on a small screen (≤ HOME_SMALL_HEIGHT_THRESHOLD), shrinks
	/// the AppHeader to a compact icon-only band so the WorkGroup/Work list has
	/// more vertical room. Restores Start-mode sizing on exit.
	/// </summary>
	void ApplyHomeModeCompactHeader()
	{
		if (Width <= 0 || Height <= 0)
			return;
		// Landscape phone: AppHeader spans rows 0-2 in the left column (Star area)
		// so the icon and "TRViS" title both have room to breathe. Restore Opacity
		// explicitly: a prior portrait Home transition may have animated AppTitle's
		// opacity down to 0; without re-setting it here, the title stays visually
		// hidden after rotating into landscape (IsVisible=true but Opacity=0).
		if (_isLandscapePhone)
		{
			AppIcon.HeightRequest = 128;
			AppIcon.WidthRequest = 128;
			AppTitle.FontSize = 36;
			AppTitle.IsVisible = true;
			AppTitle.Opacity = 1;
			AppHeader.Padding = new Thickness(16, 16, 16, 16);
			AppHeader.Spacing = 8;
			return;
		}
		if (_currentMode == PageMode.Home)
		{
			AppTitle.IsVisible = false;
			AppHeader.Padding = new Thickness(8, 4, 8, 4);
			AppHeader.Spacing = 0;
			if (IsHomeModeCompact())
			{
				AppIcon.HeightRequest = HOME_COMPACT_ICON_SIZE;
				AppIcon.WidthRequest = HOME_COMPACT_ICON_SIZE;
			}
		}
		else
		{
			if (_isCompactHeight)
			{
				AppIcon.HeightRequest = 96;
				AppIcon.WidthRequest = 96;
				AppTitle.FontSize = 32;
				AppHeader.Padding = new Thickness(16, 16, 16, 0);
				AppHeader.Spacing = 4;
			}
			else
			{
				AppIcon.HeightRequest = 160;
				AppIcon.WidthRequest = 160;
				AppTitle.FontSize = 44;
				AppHeader.Padding = new Thickness(16, 32, 16, 0);
				AppHeader.Spacing = 8;
			}
			AppTitle.IsVisible = true;
			AppTitle.Opacity = 1;
		}
	}

	// Canonical row schemes for the layered grids. All non-Star rows use fixed
	// pixel heights (scaled by _fontScale) so empty rows in BackgroundGrid stay
	// the same size as their content-bearing counterparts in StartGrid / HomeGrid.
	//
	// IMPORTANT: BackgroundGrid uses its own SEPARATE *Bg collections so the Row 0
	// animation in TransitionToAsync only moves the AppHeader band — not the body
	// grids (StartGrid / HomeGrid) that share the same scheme. Without this split,
	// pinning PortraitStartRows[0] to a pixel value during the Home→Start fade-in
	// would also shrink StartGrid's Star row, sliding StartBody (and the privacy
	// banner inside it) up and back as the row animates.
	static RowDefinitionCollection MakePortraitStartRows() => new()
	{
		new RowDefinition(GridLength.Star),
		new RowDefinition(new GridLength(0)),
		new RowDefinition(new GridLength(START_BODY_ROW_HEIGHT_FULL)),
		new RowDefinition(new GridLength(0)),
		new RowDefinition(new GridLength(FOOTER_ROW_HEIGHT)),
	};
	static RowDefinitionCollection MakePortraitHomeRows() => new()
	{
		new RowDefinition(new GridLength(HOME_HEADER_ROW_HEIGHT)),
		new RowDefinition(new GridLength(LOADER_INFO_ROW_HEIGHT)),
		new RowDefinition(GridLength.Star),
		new RowDefinition(new GridLength(HOME_BUTTONS_ROW_HEIGHT)),
		new RowDefinition(new GridLength(FOOTER_ROW_HEIGHT)),
	};
	// Stable body-grid collections: never mutated by the animation.
	static readonly RowDefinitionCollection PortraitStartRows = MakePortraitStartRows();
	static readonly RowDefinitionCollection PortraitHomeRows = MakePortraitHomeRows();
	// Animatable BackgroundGrid collections: Row 0 height is pinned to a pixel
	// value during TransitionToAsync, then reset on the next UpdateGridRowDefinitions.
	static readonly RowDefinitionCollection PortraitStartRowsBg = MakePortraitStartRows();
	static readonly RowDefinitionCollection PortraitHomeRowsBg = MakePortraitHomeRows();
	// Landscape phone:
	//   Left col   Right col
	//   Row 0 ─┐
	//   Row 1  ├─ AppHeader (icon + TRViS)         StartBody / HomeBody
	//   Row 2 ─┘   (Star: most of the column)
	//   Row 3   LoaderInfoCard                     HomeButtonsRow
	//   Row 4   (footer reserve; FooterLinks sits at RootGrid level over this)
	// Rows 0 and 1 collapse to 0 so AppHeader's RowSpan=3 covers just the Star
	// area while preserving 5-row indexing shared with the portrait collections.
	// Row 3 collapses to 0 in Start mode (no LoaderInfoCard/HomeButtonsRow shown);
	// UpdateGridRowDefinitions mutates Row[3].Height per mode.
	static readonly RowDefinitionCollection LandscapePhoneRows = new()
	{
		new RowDefinition(new GridLength(0)),
		new RowDefinition(new GridLength(0)),
		new RowDefinition(GridLength.Star),
		new RowDefinition(new GridLength(LOADER_INFO_ROW_HEIGHT)),
		new RowDefinition(new GridLength(FOOTER_ROW_HEIGHT)),
	};

	/// <summary>
	/// Points each of the three layered grids at the canonical row scheme for the
	/// current mode + orientation. StartGrid always uses PortraitStartRows, HomeGrid
	/// always uses PortraitHomeRows, BackgroundGrid uses its own *Bg variant so the
	/// Row 0 animation in TransitionToAsync doesn't drag the body grids' rows along
	/// with it. Landscape phone overrides all three to LandscapePhoneRows. Resets
	/// the BackgroundGrid Row 0 height in case a prior animation left a pixel value.
	/// </summary>
	void UpdateGridRowDefinitions(PageMode mode)
	{
		if (_isLandscapePhone)
		{
			// Start mode has nothing in Row 3 (LoaderInfoCard/HomeButtonsRow are
			// Home-only), so collapse it and give the Star row that extra space.
			LandscapePhoneRows[3].Height = mode == PageMode.Start
				? new GridLength(0)
				: new GridLength(LOADER_INFO_ROW_HEIGHT);
			BackgroundGrid.RowDefinitions = LandscapePhoneRows;
			StartGrid.RowDefinitions = LandscapePhoneRows;
			HomeGrid.RowDefinitions = LandscapePhoneRows;
			return;
		}
		PortraitStartRowsBg[0].Height = GridLength.Star;
		PortraitHomeRowsBg[0].Height = new GridLength(HOME_HEADER_ROW_HEIGHT);
		StartGrid.RowDefinitions = PortraitStartRows;
		HomeGrid.RowDefinitions = PortraitHomeRows;
		BackgroundGrid.RowDefinitions = mode == PageMode.Start ? PortraitStartRowsBg : PortraitHomeRowsBg;
	}

	/// <summary>
	/// Distributes the available HomeBody scroll-area height across the visible
	/// list(s) after subtracting the always-on overhead (section labels) and any
	/// currently-visible chips. Each list expands to fill its share so a sparse
	/// list won't push the next section below the fold, and a dense list scrolls
	/// internally within its allocated height. Floors at 2 item rows so a very
	/// tight window still leaves room to scroll.
	/// </summary>
	void UpdateListHeights()
	{
		if (_currentMode != PageMode.Home || HomeBody.Height <= 0)
			return;

		bool wgListVisible = WorkGroupListBorder.IsVisible;
		bool workListVisible = WorkListBorder.IsVisible;
		if (!wgListVisible && !workListVisible)
			return;

		// HomeBody has Padding=16,0,16,4. Bottom 4 is the only vertical chrome to
		// remove from the available height.
		const double HomeBodyBottomPad = 4.0;
		double scrollViewHeight = HomeBody.Height - HomeBodyBottomPad;
		if (scrollViewHeight <= 0)
			return;

		// Section label = "Work Group" / "Work" title above each step (always
		// rendered, Margin=2,8,2,4 + ~Subtitle line height ≈ 28-32). Chip = the
		// selected-step pill that replaces the list when a step is committed
		// (Padding=12,8 + Margin bottom 4 + 15px line ≈ 35-45). Both are slightly
		// over-budgeted so the lists never overflow into the next section.
		const double SectionLabelOverhead = 32.0;
		const double ChipOverhead = 48.0;
		const double ItemHeight = 50.0;

		double overhead = SectionLabelOverhead * 2; // WG label + Work label always shown
		if (WorkGroupChip.IsVisible) overhead += ChipOverhead;
		if (WorkChip.IsVisible) overhead += ChipOverhead;

		int listCount = (wgListVisible ? 1 : 0) + (workListVisible ? 1 : 0);
		double perList = (scrollViewHeight - overhead) / listCount;
		// Floor: 2 list rows so the list is always scrollable internally rather
		// than collapsing to a single row that can't be panned.
		double minListHeight = 2 * ItemHeight;
		perList = Math.Max(perList, minListHeight);

		if (wgListVisible) WorkGroupListView.HeightRequest = perList;
		if (workListVisible) WorkListView.HeightRequest = perList;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Subscribe here (not in ctor) so each appearance pairs with an OnDisappearing
		// unsubscribe — avoids accumulating handlers if Shell recreates the page.
		viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		viewModel.PropertyChanged += OnViewModelPropertyChanged;

		// TestSeamHost is always rendered; its 1×1 buttons are no-ops in
		// production builds (Click handlers gated on #if UI_TEST). No runtime
		// IsVisible flip — that pattern was unreliable across Mac Catalyst and
		// Windows MAUI accessibility trees.

		UpdatePrivacyDependentControls();

		// Reflect any current loader / committed selection state. If we're returning
		// here from DTAC, the user's last commit becomes their initial pending state.
		RebuildWorkGroupItems();
		SyncPendingFromCommitted();

		// We deliberately do NOT auto-load timetables from TimetableFileDirectory
		// here. The user opens files explicitly via the "ファイルを選択" button
		// (SelectFileDialog) or via a `trvis://app/open/json?local=…` AppLink.
		await ApplyModeForCurrentLoaderAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		viewModel.PropertyChanged -= OnViewModelPropertyChanged;
	}

	void UpdatePrivacyDependentControls()
	{
		// Until the privacy policy is accepted, none of the data-loading entry
		// points are usable: the reconfirm banner overlays the primary buttons
		// (so they're hidden behind it), and the demo-data button is hidden
		// outright since there is no second affordance to overlay it. After
		// acceptance the banner hides, the primary buttons reveal, and the demo
		// button reappears.
		bool accepted = InstanceManager.FirebaseSettingViewModel.IsPrivacyPolicyAccepted;
		PrivacyReconfirmBanner.IsVisible = !accepted;
		ConnectServerButton.IsEnabled = accepted;
		SelectFileButton.IsEnabled = accepted;
		LoadDemoButton.IsEnabled = accepted;
		LoadDemoButton.IsVisible = accepted;

		// Hide the Shell flyout (menu) toggle in the nav bar until the user accepts
		// the privacy policy — the body is essentially blocked by the reconfirm
		// banner anyway, and a reachable menu button is misleading. Skip Mac
		// Catalyst: its nav bar is built from FlyoutBehavior at Shell init time
		// only, so flipping post-init leaves the toggle stuck (see AppShell
		// comment). All other platforms (iOS/iPadOS, Android, Windows) re-render
		// the nav bar correctly when this flips.
		// Setting both the per-page attached property AND the Shell instance
		// property: on iOS the per-page attached property alone has been
		// observed not to repaint the existing nav bar after a value flip
		// (the toggle stays hidden after acceptance), so we also poke
		// Shell.Current.FlyoutBehavior to force a Shell-level redraw.
		// Mac Catalyst limitation: AppShell starts with FlyoutBehavior=Flyout,
		// so on Catalyst the flyout toggle is *always* visible — including
		// before privacy acceptance. ThirdPartyLicenses / Settings / Privacy
		// flyout entries are reachable, but D-TAC has no committed selection
		// pre-acceptance so it shows nothing harmful. This is a deliberate
		// trade-off vs. the alternative (no flyout reachable for the rest of
		// the session because Catalyst's nav bar can't be re-built mid-flight).
		if (!OperatingSystem.IsMacCatalyst())
		{
			var target = accepted ? FlyoutBehavior.Flyout : FlyoutBehavior.Disabled;
			Shell.SetFlyoutBehavior(this, target);
			if (Shell.Current is { } shell)
				shell.FlyoutBehavior = target;
		}
	}

	// ----- Mode / animation -----

	Task ApplyModeForCurrentLoaderAsync()
	{
		PageMode target = viewModel.Loader is null ? PageMode.Start : PageMode.Home;
		if (target == _currentMode)
		{
			// Refresh derived UI in case Loader was swapped to a new instance.
			UpdateLoaderInfoLabels();
			RefreshStepUi();
			return Task.CompletedTask;
		}
		return TransitionToAsync(target);
	}

	void ApplyHeaderLayoutInstant(PageMode mode)
	{
		UpdateGridRowDefinitions(mode);
		AppHeader.TranslationY = 0;
		AppHeader.Scale = 1.0;
		if (mode == PageMode.Start)
		{
			StartGrid.IsVisible = true;
			StartGrid.Opacity = 1;
			HomeGrid.IsVisible = false;
			HomeGrid.Opacity = 0;
		}
		else
		{
			StartGrid.IsVisible = false;
			StartGrid.Opacity = 0;
			HomeGrid.IsVisible = true;
			HomeGrid.Opacity = 1;
		}
		_currentMode = mode;
		// Apply home-mode compact header AFTER _currentMode is set (IsHomeModeCompact reads it).
		ApplyHomeModeCompactHeader();
		UpdateLoaderInfoLabels();
		RefreshStepUi();
	}

	async Task TransitionToAsync(PageMode target)
	{
		logger.Info("Transitioning page mode {0} -> {1}", _currentMode, target);

		// Portrait: we animate Row 0 between its * resolution and
		// HOME_HEADER_ROW_HEIGHT so the AppHeader (VerticalOptions=Center) glides
		// smoothly instead of snapping. Landscape-phone uses a two-column layout
		// where Row 0 is always 0 — but Row 3 (LoaderInfoCard / HomeButtonsRow band)
		// toggles between 0 (Start) and LOADER_INFO_ROW_HEIGHT (Home), so we animate
		// that row instead so the right-column body and the left-column header glide
		// to their new vertical extents rather than snapping.
		bool portraitHome = !_isLandscapePhone && Height > 0;
		bool landscapePhone = _isLandscapePhone && Height > 0;

		double startFrom, startTo, homeFrom, homeTo;
		double fromIconSize = AppIcon.HeightRequest;
		double toIconSize = fromIconSize;
		double fromTitleOpacity = AppTitle.IsVisible ? AppTitle.Opacity : 0.0;
		double toTitleOpacity = fromTitleOpacity;

		// Row 0 height animation parameters (portrait only).
		double fromRow0 = HOME_HEADER_ROW_HEIGHT;
		double toRow0 = HOME_HEADER_ROW_HEIGHT;
		bool animateRow0 = false;

		// Row 3 height animation parameters (landscape phone only). Read the
		// current value before UpdateGridRowDefinitions snaps it — this lets a
		// transition started while a prior one is mid-flight pick up from the
		// current visual height instead of jumping back to a canonical value.
		double fromRow3 = 0;
		double toRow3 = 0;
		bool animateRow3 = false;
		if (landscapePhone)
		{
			fromRow3 = LandscapePhoneRows[3].Height.Value;
			toRow3 = target == PageMode.Home ? LOADER_INFO_ROW_HEIGHT : 0;
			animateRow3 = Math.Abs(fromRow3 - toRow3) > 0.5;
		}

		// Star Row 0 resolved value in Start mode = total - fixed body row - fixed footer row.
		double startStarRow0 = Height
			- (_isCompactHeight ? START_BODY_ROW_HEIGHT_COMPACT : START_BODY_ROW_HEIGHT_FULL)
			- FOOTER_ROW_HEIGHT;

		if (target == PageMode.Home)
		{
			startFrom = StartGrid.Opacity;
			startTo = 0;
			homeFrom = 0;
			homeTo = 1;

			if (portraitHome)
			{
				toTitleOpacity = 0;
				AppHeader.Padding = new Thickness(8, 4, 8, 4);
				AppHeader.Spacing = 0;
				if (IsHomeModeCompact())
					toIconSize = HOME_COMPACT_ICON_SIZE;

				// Row 0: * (current resolved value) → HOME_HEADER_ROW_HEIGHT.
				fromRow0 = startStarRow0;
				toRow0 = HOME_HEADER_ROW_HEIGHT;
				animateRow0 = fromRow0 > toRow0 + 0.5;
			}
		}
		else
		{
			startFrom = StartGrid.Opacity;
			startTo = 1;
			homeFrom = HomeGrid.Opacity;
			homeTo = 0;

			if (portraitHome)
			{
				toIconSize = _isCompactHeight ? 96.0 : 160.0;
				toTitleOpacity = 1;
				fromTitleOpacity = 0;

				// Row 0: HOME_HEADER_ROW_HEIGHT → * (target resolved value).
				fromRow0 = HOME_HEADER_ROW_HEIGHT;
				toRow0 = startStarRow0;
				animateRow0 = toRow0 > fromRow0 + 0.5;
			}
		}

		// Set _currentMode before UpdateGridRowDefinitions and before any
		// IsVisible changes so that layout events (HomeBody.SizeChanged →
		// UpdateListHeights) see the new mode and behave correctly.
		_currentMode = target;
		UpdateLoaderInfoLabels();

		// Apply the target RowDefinitions now (Background Row 2 gets its final type:
		// * for Home, Auto for Start). This keeps UpdateListHeights stable during the
		// animation — it relies on HomeBody being in a * row when mode == Home.
		// Background Row 0 is then overridden below to its animation start value.
		UpdateGridRowDefinitions(target);

		// Ensure both grids are laid out for the cross-fade. The outgoing grid's
		// IsVisible flips to false at the end of the animation; until then it
		// stays visible with Opacity fading to 0.
		StartGrid.IsVisible = true;
		HomeGrid.IsVisible = true;
		StartGrid.Opacity = startFrom;
		HomeGrid.Opacity = homeFrom;

		if (target == PageMode.Start && portraitHome)
		{
			AppTitle.IsVisible = true;
			AppTitle.Opacity = 0;
		}

		// Pin Row 0 to the animation start value (overrides the value just set by
		// UpdateGridRowDefinitions so the transition animates smoothly from the
		// current visual position rather than snapping instantly).
		if (animateRow0)
			BackgroundGrid.RowDefinitions[0].Height = new GridLength(fromRow0);
		// Same idea for landscape Row 3: UpdateGridRowDefinitions just snapped it
		// to the target value; pin it back to fromRow3 so the animation drives it.
		// All three layered grids share the same LandscapePhoneRows instance, so
		// mutating Row[3] here animates Background, Start and Home in lockstep.
		if (animateRow3)
			LandscapePhoneRows[3].Height = new GridLength(fromRow3);

		var animation = new Animation
		{
			{ 0, 1, new Animation(v => StartGrid.Opacity = v, startFrom, startTo, Easing.CubicOut) },
			{ 0, 1, new Animation(v => HomeGrid.Opacity = v, homeFrom, homeTo, Easing.CubicOut) },
		};
		if (animateRow0)
			animation.Add(0, 1, new Animation(v => BackgroundGrid.RowDefinitions[0].Height = new GridLength(v),
				fromRow0, toRow0, Easing.CubicInOut));
		if (animateRow3)
			animation.Add(0, 1, new Animation(v => LandscapePhoneRows[3].Height = new GridLength(v),
				fromRow3, toRow3, Easing.CubicInOut));
		if (portraitHome)
		{
			if (Math.Abs(fromIconSize - toIconSize) > 0.5)
				animation.Add(0, 1, new Animation(v => { AppIcon.HeightRequest = v; AppIcon.WidthRequest = v; }, fromIconSize, toIconSize, Easing.CubicInOut));
			if (Math.Abs(fromTitleOpacity - toTitleOpacity) > 0.01)
				animation.Add(0, 1, new Animation(v => AppTitle.Opacity = v, fromTitleOpacity, toTitleOpacity, Easing.CubicOut));
		}

		var tcs = new TaskCompletionSource<bool>();
		this.Animate(
			"StartHomePage.ModeTransition",
			animation,
			length: TRANSITION_MS,
			finished: (_, cancelled) => tcs.TrySetResult(cancelled));
		bool wasCancelled = await tcs.Task;

		if (wasCancelled)
		{
			// A newer transition superseded us. Don't flip visibility here — the new
			// transition is already animating with its own from/to values.
			return;
		}

		if (target == PageMode.Home)
		{
			StartGrid.IsVisible = false;
			if (portraitHome)
				AppTitle.IsVisible = false;
			// Restore canonical Home RowDefinitions: fixes Row 0 back to the
			// HOME_HEADER_ROW_HEIGHT constant (in case the animation lambda left
			// a slightly off value) and expands Row 2 to * for HomeBody.
			UpdateGridRowDefinitions(PageMode.Home);
		}
		else
		{
			// Hide outgoing grid before restoring RowDefinitions so the * Row 0
			// resolves with only StartGrid's placeholders contributing (matching
			// toRow0 — no snap).
			HomeGrid.IsVisible = false;
			UpdateGridRowDefinitions(PageMode.Start);
			if (portraitHome)
				ApplyHomeModeCompactHeader(); // _currentMode == Start → restores full sizing
		}
	}

	void UpdateLoaderInfoLabels()
	{
		ILoader? loader = viewModel.Loader;
		if (loader is null)
		{
			LoaderInfoTitleLabel.Text = "読み込み済みデータ";
			LoaderInfoDetailLabel.Text = "";
			LoaderInfoGlyphLabel.Text = MaterialIcons.Description;
			return;
		}

		// Title = loader type, glyph = matching Material Icon, detail = source label
		// (file name, URL) set atomically with the loader via AppViewModel.SetLoader.
		(string title, string glyph) = loader switch
		{
			SampleDataLoader => ("デモデータ", MaterialIcons.Science),
			LoaderJson => ("JSON ファイル", MaterialIcons.Description),
			LoaderSQL => ("SQLite ファイル", MaterialIcons.Storage),
			WebSocketNetworkSyncService => ("サーバー接続中", MaterialIcons.Wifi),
			_ => (loader.GetType().Name, MaterialIcons.Description),
		};
		LoaderInfoTitleLabel.Text = title;
		LoaderInfoGlyphLabel.Text = glyph;
		LoaderInfoDetailLabel.Text = viewModel.LoaderSourceLabel ?? string.Empty;
	}

	// ----- Two-step picker (WorkGroup -> Work) -----
	//
	// The CollectionViews bind to ObservableCollection<WorkGroupListItem> /
	// <WorkListItem> presenters built from the active loader's lists. Selection
	// is captured in _pendingWorkGroup / _pendingWork — those drive the chip vs
	// list visual state and the 開く commit.

	void ResetPendingSelection()
	{
		_pendingWorkGroup = null;
		_pendingWork = null;
		_workItems.Clear();
	}

	void SyncPendingFromCommitted()
	{
		// Mirror the AppViewModel's *committed* selection into our tentative state.
		// Called when we appear or when the committed state changes underneath us.
		// Does not propagate back — this is intentionally one-way.
		// Compare by Id rather than Equals: WorkGroup/Work are records whose
		// auto-generated equality compares positional members (incl. byte[]
		// AffixContent / ETrainTimetableContent on Work) by reference. After a
		// websocket Refresh, the manager's new instance won't reference-match
		// the cached one even though it represents the same logical row, so
		// Equals returns false and we'd needlessly rebuild lists.
		var committedWG = viewModel.SelectedWorkGroup;
		var committedWork = viewModel.SelectedWork;

		if (!IsSameWorkGroup(_pendingWorkGroup, committedWG))
		{
			_pendingWorkGroup = committedWG;
			RebuildWorkItems();
		}
		if (!IsSameWork(_pendingWork, committedWork))
		{
			_pendingWork = committedWork;
		}

		SyncListViewSelections();
		RefreshStepUi();
	}

	static bool IsSameWorkGroup(WorkGroup? a, WorkGroup? b)
	{
		if (ReferenceEquals(a, b))
			return true;
		if (a is null || b is null)
			return false;
		return string.Equals(a.Id, b.Id, StringComparison.Ordinal);
	}

	static bool IsSameWork(Work? a, Work? b)
	{
		if (ReferenceEquals(a, b))
			return true;
		if (a is null || b is null)
			return false;
		return string.Equals(a.Id, b.Id, StringComparison.Ordinal);
	}

	void SyncListViewSelections()
	{
		// Reflect _pendingWorkGroup / _pendingWork onto the CollectionViews without
		// re-triggering OnXxxSelectionChanged (which would reset the user-pick flow).
		// Match by Id (see SyncPendingFromCommitted for why record Equals is unsafe).
		_suppressSelectionChanged = true;
		try
		{
			WorkGroupListView.SelectedItem = _pendingWorkGroup is null
				? null
				: _workGroupItems.FirstOrDefault(i => IsSameWorkGroup(i.Source, _pendingWorkGroup));
			WorkListView.SelectedItem = _pendingWork is null
				? null
				: _workItems.FirstOrDefault(i => IsSameWork(i.Source, _pendingWork));
		}
		finally
		{
			_suppressSelectionChanged = false;
		}
	}

	void EnsureCountCacheLoader(ILoader? loader)
	{
		if (ReferenceEquals(_countCacheLoader, loader))
			return;
		_countCacheLoader = loader;
		InvalidateCountCaches();
	}

	void InvalidateCountCaches()
	{
		// Called whenever the underlying lists may have changed: loader swap or
		// WorkGroupList PropertyChanged (which fires after a websocket Refresh
		// that may have added/removed Works/Trains).
		_workCountByGroupId.Clear();
		_trainCountByWorkId.Clear();
	}

	int GetWorkCountCached(ILoader loader, string workGroupId)
	{
		if (_workCountByGroupId.TryGetValue(workGroupId, out int cached))
			return cached;
		int count;
		try { count = loader.GetWorkList(workGroupId).Count; }
		catch { count = 0; }
		_workCountByGroupId[workGroupId] = count;
		return count;
	}

	int GetTrainCountCached(ILoader loader, string workId)
	{
		if (_trainCountByWorkId.TryGetValue(workId, out int cached))
			return cached;
		int count;
		try { count = loader.GetTrainDataList(workId).Count; }
		catch { count = 0; }
		_trainCountByWorkId[workId] = count;
		return count;
	}

	void RebuildWorkGroupItems()
	{
		// See RebuildWorkItems for the iOS 12 detach/reattach rationale.
		bool detachForIos12CollectionViewCrash =
#if IOS
			!OperatingSystem.IsIOSVersionAtLeast(13);
#else
			false;
#endif
		if (detachForIos12CollectionViewCrash)
			WorkGroupListView.ItemsSource = null;
		try
		{
			_workGroupItems.Clear();
			var loader = viewModel.Loader;
			var groups = viewModel.WorkGroupList;
			EnsureCountCacheLoader(loader);
			if (loader is not null && groups is not null)
			{
				foreach (var wg in groups)
				{
					int workCount = GetWorkCountCached(loader, wg.Id);
					string subtitle = $"Work 数: {workCount}";
					_workGroupItems.Add(new WorkGroupListItem(wg, wg.Name, subtitle));
				}
			}
		}
		finally
		{
			if (detachForIos12CollectionViewCrash)
				WorkGroupListView.ItemsSource = _workGroupItems;
		}
		// Cascade to Work list — runs regardless of loader/groups state so the
		// Work list mirrors the (possibly cleared) WorkGroup list.
		RebuildWorkItems();
	}

	void RebuildWorkItems()
	{
		// iOS 12: detaching ItemsSource around the mutation forces MAUI to issue
		// a single ReloadData instead of the per-Add InsertItems calls that
		// ObservableItemsSource normally emits. The latter path crashes the app
		// the first time a WorkGroup is picked because WorkListView is
		// transitioning from IsVisible=false to true, and an InsertItems call
		// mid-layout-pass triggers UICollectionViewFlowLayout to be invalidated
		// with a (null) context — iOS 13+ tolerates that, iOS 12 rejects it as
		// NSInvalidArgumentException. See log 2026-05-11 15:35:39.
		bool detachForIos12CollectionViewCrash =
#if IOS
			!OperatingSystem.IsIOSVersionAtLeast(13);
#else
			false;
#endif
		if (detachForIos12CollectionViewCrash)
			WorkListView.ItemsSource = null;
		try
		{
			_workItems.Clear();
			var loader = viewModel.Loader;
			var wg = _pendingWorkGroup;
			if (loader is null || wg is null)
				return;
			EnsureCountCacheLoader(loader);

			IReadOnlyList<Work> works;
			try { works = loader.GetWorkList(wg.Id); }
			catch { works = Array.Empty<Work>(); }

			foreach (var w in works)
			{
				int trainCount = GetTrainCountCached(loader, w.Id);

				List<string> parts = new(2);
				if (w.AffectDate is { } d)
					parts.Add($"施行日: {d:yyyy/MM/dd}");
				parts.Add($"列車数: {trainCount}");
				_workItems.Add(new WorkListItem(w, w.Name, string.Join(" · ", parts)));
			}
		}
		finally
		{
			if (detachForIos12CollectionViewCrash)
				WorkListView.ItemsSource = _workItems;
		}
	}

	void RefreshStepUi()
	{
		var pendingWG = _pendingWorkGroup;
		var pendingW = _pendingWork;

		bool hasWorkGroup = pendingWG is not null;
		WorkGroupChip.IsVisible = hasWorkGroup;
		WorkGroupListBorder.IsVisible = !hasWorkGroup;
		WorkGroupChipNameLabel.Text = pendingWG?.Name ?? string.Empty;

		bool hasWork = pendingW is not null;
		WorkChip.IsVisible = hasWorkGroup && hasWork;
		WorkListBorder.IsVisible = hasWorkGroup && !hasWork;
		WorkChipNameLabel.Text = pendingW?.Name ?? string.Empty;

		UpdateListHeights();
	}

	void OnWorkGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged)
			return;
		try
		{
			var item = WorkGroupListView.SelectedItem as WorkGroupListItem;
			_pendingWorkGroup = item?.Source;
			// Switching Work Group invalidates any prior Work pick and rebuilds the
			// Work list for the new group.
			_pendingWork = null;
			RebuildWorkItems();
			SyncListViewSelections();
			RefreshStepUi();
		}
		catch (Exception ex)
		{
			// UI-thread event handler — an exception that escapes here propagates
			// through Mono's unhandled exception hook and aborts the process. Log
			// + Crashlytics + sync-flush before rethrowing so the crash record is
			// recoverable. (See MauiProgram.CurrentDomain_UnhandledException for
			// the matching app-level flush.)
			logger.Error(ex, "OnWorkGroupSelectionChanged failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnWorkGroupSelectionChanged");
			try { NLog.LogManager.Flush(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
			throw;
		}
	}

	void OnWorkSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged)
			return;
		try
		{
			var item = WorkListView.SelectedItem as WorkListItem;
			_pendingWork = item?.Source;
			RefreshStepUi();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "OnWorkSelectionChanged failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnWorkSelectionChanged");
			try { NLog.LogManager.Flush(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
			throw;
		}
	}

	void OnWorkGroupChipTapped(object? sender, TappedEventArgs e)
	{
		logger.Info("Work Group chip tapped -> clearing tentative selection");
		_pendingWorkGroup = null;
		_pendingWork = null;
		_workItems.Clear();
		SyncListViewSelections();
		RefreshStepUi();
	}

	void OnWorkChipTapped(object? sender, TappedEventArgs e)
	{
		logger.Info("Work chip tapped -> clearing tentative selection");
		_pendingWork = null;
		SyncListViewSelections();
		RefreshStepUi();
	}

	// ----- Button handlers -----

	async void OnConnectServerClicked(object sender, EventArgs e)
	{
		logger.Info("Connect Server clicked");

		try
		{
			await Navigation.PushModalAsync(new ConnectServerDialog());
		}
		catch (Exception ex)
		{
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnConnectServerClicked (PushModalAsync failed)");
			logger.Error(ex, "PushModalAsync failed");
			await Util.DisplayAlertAsync(this, "Open Popup Failed", ex.ToString(), "OK");
		}
	}

	async void OnSelectFileClicked(object sender, EventArgs e)
	{
		logger.Info("Select File clicked");

		try
		{
			await Navigation.PushModalAsync(new SelectFileDialog());
		}
		catch (Exception ex)
		{
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnSelectFileClicked (PushModalAsync failed)");
			logger.Error(ex, "PushModalAsync failed");
			await Util.DisplayAlertAsync(this, "Open Dialog Failed", ex.ToString(), "OK");
		}
	}

	async void OnLoadDemoClicked(object sender, EventArgs e)
	{
		logger.Info("Load Demo clicked");

		try
		{
			// Dispose AFTER the new loader is built so any in-flight property
			// reads on viewModel.Loader during the await don't hit a disposed
			// instance. SetLoader swaps atomically; we then dispose what was
			// previously installed.
			ILoader? previous = viewModel.Loader;
			var newLoader = await SampleDataLoader.CreateAsync();
			viewModel.SetLoader(newLoader, null);
			if (!ReferenceEquals(previous, viewModel.Loader))
				previous?.Dispose();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Load demo failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnLoadDemoClicked (CreateAsync failed)");
			await Util.DisplayAlertAsync(this, "エラー", $"サンプルデータの読み込みに失敗しました: {ex.Message}", "OK");
		}
	}

	async void OnPrivacyPolicyClicked(object sender, EventArgs e)
	{
		logger.Info("Privacy Policy clicked");
		try
		{
			await Navigation.PushModalAsync(new PrivacyPolicyDialog());
			// After modal closes, banner + button enabled-state may have changed.
			UpdatePrivacyDependentControls();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PushModalAsync(PrivacyPolicyDialog) failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnPrivacyPolicyClicked");
		}
	}

	async void OnThirdPartyLicensesClicked(object sender, EventArgs e)
	{
		logger.Info("Third Party Licenses clicked");
		try
		{
			// asModal:true makes the in-page Close header visible so the modal can be
			// dismissed reliably across platforms (NavigationPage toolbar items render
			// inconsistently on Mac Catalyst).
			var page = new ThirdPartyLicenses(asModal: true);
			await Navigation.PushModalAsync(page);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PushModalAsync(ThirdPartyLicenses) failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.OnThirdPartyLicensesClicked");
		}
	}

	async void OnOpenClicked(object sender, EventArgs e)
	{
		logger.Info("Open clicked");
		var pendingWG = _pendingWorkGroup;
		var pendingW = _pendingWork;

		if (pendingWG is null || pendingW is null)
		{
			logger.Info("Open ignored: pending selection incomplete (WG={0}, W={1})", pendingWG, pendingW);
			await Util.DisplayAlertAsync(this, "選択されていません", "Work Group と Work を選択してから「開く」を押してください。", "OK");
			return;
		}

		CommitPendingSelection(pendingWG, pendingW);
		await NavigateToDTACAsync();
	}

	// Commit tentative -> AppViewModel. Setting SelectedWorkGroup cascades
	// (TimetableSelectionManager.OnWorkGroupChanged auto-picks the first Work
	// of that group); we then immediately overwrite with the user's pending
	// Work, which cascades again to pick its first TrainData. Net effect:
	// committed (WG, W, first-Train) — the same shape DTAC has always seen.
	// Wrapped in _committingOpen so the cascade-fired SelectedWork PropertyChanged
	// doesn't yank our pending state via SyncPendingFromCommitted.
	void CommitPendingSelection(WorkGroup workGroup, Work work)
	{
		_committingOpen = true;
		try
		{
			viewModel.SelectedWorkGroup = workGroup;
			viewModel.SelectedWork = work;
		}
		finally
		{
			_committingOpen = false;
		}
	}

	async void OnDisconnectClicked(object sender, EventArgs e)
	{
		logger.Info("Disconnect/Close clicked");
		if (viewModel.Loader is null)
			return;

		bool confirm = await Util.DisplayAlertAsync(this, "確認", "現在のデータを閉じますか？", "閉じる", "キャンセル");
		if (!confirm)
			return;

		// Re-read Loader AFTER the await: an AppLink or websocket reconnect could
		// have swapped (and disposed) the old one while the confirm dialog was
		// open. Disposing the captured value would then double-dispose the old
		// loader and orphan the new one.
		var loader = viewModel.Loader;
		if (loader is null)
			return;

		viewModel.Loader = null;
		loader.Dispose();
		// Loader change triggers OnViewModelPropertyChanged -> animate back to Start mode.
	}

	// ----- Helpers -----

	private static async Task NavigateToDTACAsync()
	{
		try
		{
			logger.Info("Navigating to DTAC page");
			await Shell.Current.GoToAsync($"//{ViewHost.NameOfThisClass}");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error navigating to DTAC page");
			InstanceManager.CrashlyticsWrapper.Log(ex, "StartHomePage.NavigateToDTACAsync failed");
		}
	}

	// ----- Test seams -----

	void TestSeedButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedButton clicked: seeding URL history fixtures");
		viewModel.SeedUrlHistoryForTesting(new[]
		{
			"https://example.com/timetable-a.json",
			"https://example.com/timetable-b.json",
		});
#endif
	}

	void TestOpenSelectFileDialogButton_Clicked(object? sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestOpenSelectFileDialogButton clicked: invoking OnSelectFileClicked directly");
		// Same code path as the visible SelectFileButton's Clicked handler — the
		// real handler pushes Navigation.PushModalAsync(new SelectFileDialog())
		// and logs/handles failures. Routing through it (vs. duplicating the
		// PushModalAsync) keeps this seam honest: if OnSelectFileClicked ever
		// changes shape, this seam tracks it without per-test rewrites.
		OnSelectFileClicked(sender!, e);
#endif
	}

	async void TestAutoOpenButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestAutoOpenButton clicked: auto-pick first WG/Work and navigate to DTAC");
		try
		{
			// Mimic the user flow exactly: pick first WG, materialize its Work list,
			// pick first Work, then commit through the same code path 開く uses.
			// This keeps the seam honest as a "skip the picker UI" shortcut, not a
			// parallel path that could survive a refactor of the real Open button.
			var groups = viewModel.WorkGroupList;
			var firstGroup = groups?.FirstOrDefault();
			if (firstGroup is null)
			{
				logger.Warn("TestAutoOpenButton: no WorkGroup available — ignoring");
				return;
			}
			var loader = viewModel.Loader;
			if (loader is null)
				return;
			var firstWork = loader.GetWorkList(firstGroup.Id)?.FirstOrDefault();
			if (firstWork is null)
			{
				logger.Warn("TestAutoOpenButton: first WorkGroup has no Work — aborting navigate");
				return;
			}

			CommitPendingSelection(firstGroup, firstWork);
			await NavigateToDTACAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestAutoOpenButton failed");
		}
#else
		await Task.CompletedTask;
#endif
	}

	void TestSeedGpsButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedGpsButton clicked: pushing fixture GPS coord");
		try
		{
			var locationService = InstanceManager.LocationService;
			locationService.SetLonLatLocationService();
			locationService.SetGpsLocation(longitude: 139.790, latitude: 35.700, accuracy: 10.0, useAverageDistance: false);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedGpsButton failed");
		}
#endif
	}

	void TestClearHistoryButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestClearHistoryButton clicked: clearing URL history");
		try
		{
			viewModel.ClearUrlHistoryForTesting();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestClearHistoryButton failed");
		}
#endif
	}

	async void TestSeedHorizontalTimetableButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedHorizontalTimetableButton clicked: seed horizontal timetable + navigate to DTAC");
		try
		{
			var groups = viewModel.WorkGroupList;
			var firstGroup = groups?.FirstOrDefault();
			if (firstGroup is null)
			{
				logger.Warn("TestSeedHorizontalTimetable: no WorkGroup available — ignoring");
				return;
			}
			var loader = viewModel.Loader;
			if (loader is null)
				return;
			var firstWork = loader.GetWorkList(firstGroup.Id)?.FirstOrDefault();
			if (firstWork is null)
			{
				logger.Warn("TestSeedHorizontalTimetable: first WorkGroup has no Work — aborting");
				return;
			}

			// 1×1 transparent PNG. Smallest valid PNG; renders in any WebView.
			byte[] tinyPng = Convert.FromBase64String(
				"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgAAIAAAUAAeImBZsAAAAASUVORK5CYII=");
			var seededWork = firstWork with
			{
				HasETrainTimetable = true,
				ETrainTimetableContentType = (int)TRViS.IO.Models.ContentType.PNG,
				ETrainTimetableContent = tinyPng,
			};

			CommitPendingSelection(firstGroup, seededWork);
			await NavigateToDTACAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedHorizontalTimetableButton failed");
		}
#else
		await Task.CompletedTask;
#endif
	}

	// Seeds a minimal SQLite fixture into TimetableFileDirectory using the same
	// sqlite-net write path that LoaderSQL uses to read. The point is to exercise
	// SQLitePCLRaw provider initialization inside the live MAUI runtime — the
	// netcore-based TRViS.IO.Tests don't go through MAUI's linker/AOT, so a
	// missing Batteries_V2.Init or stripped provider registration only ever
	// surfaces here. If the seed step throws, no file appears, the SelectFile
	// dialog renders the empty state, and the corresponding test fails with a
	// "card not visible" assertion that points back at this seam.
	void TestSeedSqliteButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedSqliteButton clicked: seeding minimal SQLite fixture");
		try
		{
			if (!DirectoryPathProvider.TimetableFileDirectory.Exists)
				Directory.CreateDirectory(DirectoryPathProvider.TimetableFileDirectory.FullName);

			string path = Path.Combine(
				DirectoryPathProvider.TimetableFileDirectory.FullName,
				UITestSqliteFixtureFileName);
			if (File.Exists(path))
				File.Delete(path);

			using var cnx = new SQLite.SQLiteConnection(path);
			cnx.CreateTable<IO.Models.DB.WorkGroup>();
			cnx.Insert(new IO.Models.DB.WorkGroup
			{
				Id = "1",
				Name = "UITestWG",
				DBVersion = 1,
			});
			logger.Info("Seeded SQLite fixture at {0}", path);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedSqliteButton failed");
		}
#endif
	}

	// Wipes TimetableFileDirectory contents so SelectFile-related tests can
	// guarantee a known starting state without relying on platform-specific
	// app-data wipe (Mac Catalyst / iOS keep the documents folder across
	// noReset:true sessions).
	void TestClearTimetablesButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestClearTimetablesButton clicked: clearing TimetableFileDirectory");
		try
		{
			DirectoryInfo dir = DirectoryPathProvider.TimetableFileDirectory;
			if (dir.Exists)
			{
				foreach (FileInfo file in dir.GetFiles())
					file.Delete();
				foreach (DirectoryInfo sub in dir.GetDirectories())
					sub.Delete(recursive: true);
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestClearTimetablesButton failed");
		}
#endif
	}

	// Filename used by the UI_TEST seed seam. Public so the test fixture can
	// reference the same constant when looking up the rendered card by id.
	public const string UITestSqliteFixtureFileName = "uitest_seed.sqlite";

	void TestSeedSampleFilesButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedSampleFilesButton clicked: seeding SelectFileDialog fixtures");
		try
		{
			SelectFileDialogTestSeams.SeedSampleFiles();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedSampleFilesButton failed");
		}
#endif
	}

	void TestClearSampleFilesButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestClearSampleFilesButton clicked: clearing SelectFileDialog fixtures + FilePicker override");
		try
		{
			SelectFileDialogTestSeams.ClearSampleFiles();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestClearSampleFilesButton failed");
		}
#endif
	}

	void TestClearLoaderButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestClearLoaderButton clicked: clearing in-memory AppViewModel.Loader");
		try
		{
			// Mirror OnDisconnectClicked's clear path but without the confirm
			// dialog — tests assume "yes, throw away the loader" and the
			// dialog would otherwise need to be pumped on each platform's
			// driver. Setting Loader=null triggers
			// OnViewModelPropertyChanged → ApplyModeForCurrentLoaderAsync,
			// which animates the page back to Start mode (StartBody visible,
			// HomeBody hidden) so LoadDemoButton becomes clickable again.
			var previous = viewModel.Loader;
			viewModel.Loader = null;
			previous?.Dispose();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestClearLoaderButton failed");
		}
#endif
	}

	void TestSetupBrowseFallbackButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSetupBrowseFallbackButton clicked: installing FilePicker override");
		try
		{
			SelectFileDialogTestSeams.SetupBrowseFallback();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSetupBrowseFallbackButton failed");
		}
#endif
	}

	/// <summary>
	/// Test seam: tapped from UI tests via "StartHome.TestSeedNextTrainSelectionButton".
	/// Commits selection to a sample-data train whose NextTrainId is non-empty
	/// (WorkGroup "hako-order-test" → Work "work-linear" → Train "linear-train-1",
	/// NextTrainId = "linear-train-2") and navigates to DTAC. Mirrors the
	/// TestAutoOpenButton pattern but targets a specific Work so the regression
	/// test for #225 doesn't rely on the default first train (which has an empty
	/// NextTrainId).
	/// </summary>
	async void TestSeedNextTrainSelectionButton_Clicked(object sender, EventArgs e)
	{
#if UI_TEST
		logger.Info("TestSeedNextTrainSelection clicked: committing linear-train-1 and navigating to DTAC");
		try
		{
			var wg = viewModel.WorkGroupList?.FirstOrDefault(w => w.Id == "hako-order-test");
			if (wg is null)
			{
				logger.Warn("TestSeedNextTrainSelection: hako-order-test WorkGroup not found");
				return;
			}
			var loader = viewModel.Loader;
			if (loader is null)
				return;
			var work = loader.GetWorkList(wg.Id)?.FirstOrDefault(w => w.Id == "work-linear");
			if (work is null)
			{
				logger.Warn("TestSeedNextTrainSelection: work-linear not found under hako-order-test");
				return;
			}

			CommitPendingSelection(wg, work);
			await NavigateToDTACAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestSeedNextTrainSelection failed");
		}
#else
		await Task.CompletedTask;
#endif
	}
}
