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

	// Animation tunables. Header is centered slightly above middle in Start mode;
	// pinned to top in Home mode.
	// START_HEADER_CENTER_FRACTION places the *center* of the header at this fraction
	// of the page height, so the visual "centeredness" stays consistent regardless of
	// header size (icon bigger/smaller, accessibility font scale).
	const double START_HEADER_CENTER_FRACTION = 0.32; // 0.5 = middle. 0.32 = "slightly above middle".
	// AppHeader has AnchorY=0 so Scale shrinks downward, keeping the visual top at the same y.
	// HOME_HEADER_SCALE controls the visual size of the Home-mode header band; smaller = more
	// vertical space for the WorkGroup/Work selection lists below.
	const double HOME_HEADER_SCALE = 0.55;
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

	double ComputeStartHeaderTranslationY()
	{
		// In landscape phone, AppHeader sits at the top of its dedicated left
		// column — no translation is needed to keep AppTitle visible.
		if (_isLandscapePhone)
			return 0;
		double pageHeight = Height > 0 ? Height : InstanceManager.AppViewModel.WindowHeight;
		double headerHeight = AppHeader.Height > 0 ? AppHeader.Height : 220;
		double bodyHeight = StartBody.Height > 0 ? StartBody.Height : 0;
		// RootGrid uses RowDefinitions="Auto,*,Auto"; AppHeader lives in Row 0
		// at its natural height. Translate downward so its center lands at
		// START_HEADER_CENTER_FRACTION of the available space above StartBody.
		double upperRegion = Math.Max(headerHeight, pageHeight - bodyHeight);
		double centered = (upperRegion * START_HEADER_CENTER_FRACTION) - (headerHeight / 2);
		double maxTranslation = upperRegion - headerHeight;
		return Math.Clamp(centered, 0, Math.Max(0, maxTranslation));
	}

	public StartHomePage()
	{
		logger.Trace("Creating");

		InitializeComponent();

		viewModel = InstanceManager.AppViewModel;
		BindingContext = viewModel;

		WorkGroupListView.ItemsSource = _workGroupItems;
		WorkListView.ItemsSource = _workItems;

		// Apply initial header layout once we have a measured size.
		SizeChanged += OnSizeChangedFirstLayout;
		AppHeader.SizeChanged += (_, __) =>
		{
			UpdateHomeBodyTopSpacer();
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

		logger.Trace("Created");
	}

	void UpdateHomeBodyTopSpacer()
	{
		// In landscape phone HomeBody sits in its own column to the right of
		// the header — no top spacer needed because nothing renders above it.
		if (_isLandscapePhone)
		{
			HomeBodyTopSpacer.HeightRequest = 0;
			return;
		}
		// Portrait: HomeBody RowSpan=3 covers the AppHeader and LoaderInfoCard
		// rows too, so reserve enough space at the top of HomeBody to keep
		// its ScrollView clear of *both* upper rows. The card is declared
		// before HomeBody and would otherwise be z-ordered behind it; the
		// transparent spacer area lets it (and the AppHeader) show through,
		// and the picker starts below it. We use the full AppHeader.Height
		// (not the scaled visual) because Row 0 of RootGrid sizes to the
		// unscaled header regardless of Scale, and the spacer must cover
		// the whole row.
		double headerHeight = AppHeader.Height > 0 ? AppHeader.Height : 220;
		double loaderInfoHeight = LoaderInfoCard.IsVisible
			? (LoaderInfoCard.Height > 0 ? LoaderInfoCard.Height : 80)
			: 0;
		HomeBodyTopSpacer.HeightRequest = headerHeight + loaderInfoHeight + 12;
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
		// RowDefinitions / ColumnDefinitions live in XAML — only adjust each
		// child's Grid.Row / Column / Span attached properties here.
		if (isLandscapePhone)
		{
			// Phone landscape: AppHeader and LoaderInfoCard share Row 0 in the
			// left column (header at the top, loader-info card pinned to the
			// bottom of the row via VerticalOptions=End — Home mode shrinks
			// the header to ~55% of its natural height which leaves room
			// below it for the card without overflowing into Row 1). Row 1
			// is empty in the left column and the Auto height of Row 2 holds
			// the FooterLinks. StartBody / HomeBody fill the right column
			// across all three rows so Open/Close land on the same y as
			// FooterLinks at the bottom-left.
			Grid.SetRow(AppHeader, 0); Grid.SetRowSpan(AppHeader, 1);
			Grid.SetColumn(AppHeader, 0); Grid.SetColumnSpan(AppHeader, 1);
			Grid.SetRow(LoaderInfoCard, 0); Grid.SetRowSpan(LoaderInfoCard, 1);
			Grid.SetColumn(LoaderInfoCard, 0); Grid.SetColumnSpan(LoaderInfoCard, 1);
			LoaderInfoCard.VerticalOptions = LayoutOptions.End;
			Grid.SetRow(StartBody, 0); Grid.SetRowSpan(StartBody, 3);
			Grid.SetColumn(StartBody, 1); Grid.SetColumnSpan(StartBody, 1);
			StartBody.VerticalOptions = LayoutOptions.Center;
			Grid.SetRow(HomeBody, 0); Grid.SetRowSpan(HomeBody, 3);
			Grid.SetColumn(HomeBody, 1); Grid.SetColumnSpan(HomeBody, 1);
			Grid.SetRow(FooterLinks, 2); Grid.SetRowSpan(FooterLinks, 1);
			Grid.SetColumn(FooterLinks, 0); Grid.SetColumnSpan(FooterLinks, 1);
			Grid.SetRow(TestSeamHost, 0); Grid.SetColumn(TestSeamHost, 0);
			UpdateHomeBodyTopSpacer();
		}
		else
		{
			// Portrait / tablet: every visible element spans both columns.
			// HomeBody RowSpan=2 covers the AppHeader row + buttons row so
			// the picker keeps room and the LoaderInfoCard / AppHeader render
			// on top of HomeBody's transparent top-spacer area.
			Grid.SetRow(AppHeader, 0); Grid.SetRowSpan(AppHeader, 1);
			Grid.SetColumn(AppHeader, 0); Grid.SetColumnSpan(AppHeader, 2);
			Grid.SetRow(LoaderInfoCard, 0); Grid.SetRowSpan(LoaderInfoCard, 1);
			Grid.SetColumn(LoaderInfoCard, 0); Grid.SetColumnSpan(LoaderInfoCard, 2);
			LoaderInfoCard.VerticalOptions = LayoutOptions.End;
			Grid.SetRow(StartBody, 1); Grid.SetRowSpan(StartBody, 1);
			Grid.SetColumn(StartBody, 0); Grid.SetColumnSpan(StartBody, 2);
			StartBody.VerticalOptions = LayoutOptions.End;
			Grid.SetRow(HomeBody, 0); Grid.SetRowSpan(HomeBody, 2);
			Grid.SetColumn(HomeBody, 0); Grid.SetColumnSpan(HomeBody, 2);
			Grid.SetRow(FooterLinks, 2); Grid.SetRowSpan(FooterLinks, 1);
			Grid.SetColumn(FooterLinks, 0); Grid.SetColumnSpan(FooterLinks, 2);
			Grid.SetRow(TestSeamHost, 0); Grid.SetColumn(TestSeamHost, 0);
			UpdateHomeBodyTopSpacer();
		}
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
		// Only compact in portrait/square layouts. Landscape-phone has its own
		// horizontal split layout (header column + body column) which already
		// avoids the overlap, so leave it at full size.
		bool isPortrait = !_isLandscapePhone && Width <= Height;
		bool isCompact;
		if (!_compactHeightApplied)
		{
			// First measurement: pick the appropriate side of the band based
			// solely on the enter threshold, then both sticky branches below
			// will hold us there until we cross the opposite threshold.
			isCompact = isPortrait && Height < COMPACT_HEIGHT_ENTER;
		}
		else if (_isCompactHeight)
		{
			// Currently compact: exit only when we comfortably clear EXIT.
			isCompact = isPortrait && Height < COMPACT_HEIGHT_EXIT;
		}
		else
		{
			// Currently full-size: enter compact only below the lower threshold.
			isCompact = isPortrait && Height < COMPACT_HEIGHT_ENTER;
		}
		if (isCompact == _isCompactHeight && _compactHeightApplied)
			return;
		_isCompactHeight = isCompact;
		_compactHeightApplied = true;

		if (isCompact)
		{
			AppIcon.HeightRequest = 96;
			AppIcon.WidthRequest = 96;
			AppTitle.FontSize = 32;
			AppHeader.Padding = new Thickness(16, 16, 16, 0);
			AppHeader.Spacing = 4;
			ConnectServerButton.HeightRequest = 56;
			ConnectServerButton.FontSize = 17;
			SelectFileButton.HeightRequest = 56;
			SelectFileButton.FontSize = 17;
			LoadDemoButton.HeightRequest = 36;
			StartBody.Padding = new Thickness(24, 4, 24, 8);
			StartBody.RowSpacing = 4;
		}
		else
		{
			AppIcon.HeightRequest = 160;
			AppIcon.WidthRequest = 160;
			AppTitle.FontSize = 44;
			AppHeader.Padding = new Thickness(16, 32, 16, 0);
			AppHeader.Spacing = 8;
			ConnectServerButton.HeightRequest = 80;
			ConnectServerButton.FontSize = 20;
			SelectFileButton.HeightRequest = 80;
			SelectFileButton.FontSize = 20;
			LoadDemoButton.HeightRequest = 44;
			StartBody.Padding = new Thickness(24, 8, 24, 24);
			StartBody.RowSpacing = 8;
		}
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
		UpdateHomeBodyTopSpacer();

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
		if (mode == PageMode.Start)
		{
			AppHeader.TranslationY = ComputeStartHeaderTranslationY();
			AppHeader.Scale = 1.0;
			StartBody.IsVisible = true;
			StartBody.Opacity = 1;
			HomeBody.IsVisible = false;
			HomeBody.Opacity = 0;
			LoaderInfoCard.IsVisible = false;
			LoaderInfoCard.Opacity = 0;
		}
		else
		{
			AppHeader.TranslationY = 0;
			AppHeader.Scale = HOME_HEADER_SCALE;
			StartBody.IsVisible = false;
			StartBody.Opacity = 0;
			HomeBody.IsVisible = true;
			HomeBody.Opacity = 1;
			LoaderInfoCard.IsVisible = true;
			LoaderInfoCard.Opacity = 1;
		}
		_currentMode = mode;
		UpdateLoaderInfoLabels();
		RefreshStepUi();
	}

	async Task TransitionToAsync(PageMode target)
	{
		logger.Info("Transitioning page mode {0} -> {1}", _currentMode, target);

		double centeredOffset = ComputeStartHeaderTranslationY();
		double headerFromY, headerToY, headerFromScale, headerToScale;
		double startBodyFrom, startBodyTo, homeBodyFrom, homeBodyTo;

		if (target == PageMode.Home)
		{
			headerFromY = AppHeader.TranslationY;
			headerToY = 0;
			headerFromScale = AppHeader.Scale;
			headerToScale = HOME_HEADER_SCALE;
			startBodyFrom = StartBody.Opacity;
			startBodyTo = 0;
			homeBodyFrom = 0;
			homeBodyTo = 1;

			HomeBody.Opacity = 0;
			HomeBody.IsVisible = true;
			LoaderInfoCard.Opacity = 0;
			LoaderInfoCard.IsVisible = true;
		}
		else
		{
			headerFromY = AppHeader.TranslationY;
			headerToY = centeredOffset;
			headerFromScale = AppHeader.Scale;
			headerToScale = 1.0;
			startBodyFrom = StartBody.Opacity;
			startBodyTo = 1;
			homeBodyFrom = HomeBody.Opacity;
			homeBodyTo = 0;

			StartBody.Opacity = startBodyFrom;
			StartBody.IsVisible = true;
		}

		_currentMode = target;
		UpdateLoaderInfoLabels();

		double loaderInfoFrom = LoaderInfoCard.Opacity;
		double loaderInfoTo = target == PageMode.Home ? 1 : 0;
		var animation = new Animation
		{
			{ 0, 1, new Animation(v => AppHeader.TranslationY = v, headerFromY, headerToY, Easing.CubicInOut) },
			{ 0, 1, new Animation(v => AppHeader.Scale = v, headerFromScale, headerToScale, Easing.CubicInOut) },
			{ 0, 1, new Animation(v => StartBody.Opacity = v, startBodyFrom, startBodyTo, Easing.CubicOut) },
			{ 0, 1, new Animation(v => HomeBody.Opacity = v, homeBodyFrom, homeBodyTo, Easing.CubicOut) },
			{ 0, 1, new Animation(v => LoaderInfoCard.Opacity = v, loaderInfoFrom, loaderInfoTo, Easing.CubicOut) },
		};

		var tcs = new TaskCompletionSource<bool>();
		this.Animate(
			"StartHomePage.ModeTransition",
			animation,
			length: TRANSITION_MS,
			// finished signature: (final-progress, was-cancelled). When cancelled the
			// follow-up visibility flip below is skipped — but the *next* TransitionToAsync
			// runs ApplyHeaderLayoutInstant via fall-through if it's the same target,
			// or sets the right visibility on its own animation completion if different.
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
			StartBody.IsVisible = false;
		}
		else
		{
			HomeBody.IsVisible = false;
			LoaderInfoCard.IsVisible = false;
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
		_workGroupItems.Clear();
		var loader = viewModel.Loader;
		var groups = viewModel.WorkGroupList;
		EnsureCountCacheLoader(loader);
		if (loader is null || groups is null)
		{
			RebuildWorkItems();
			return;
		}
		foreach (var wg in groups)
		{
			int workCount = GetWorkCountCached(loader, wg.Id);
			string subtitle = $"Work 数: {workCount}";
			_workGroupItems.Add(new WorkGroupListItem(wg, wg.Name, subtitle));
		}
		RebuildWorkItems();
	}

	void RebuildWorkItems()
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
		WorkPendingHint.IsVisible = !hasWorkGroup;
		WorkChipNameLabel.Text = pendingW?.Name ?? string.Empty;
	}

	void OnWorkGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged)
			return;
		var item = WorkGroupListView.SelectedItem as WorkGroupListItem;
		_pendingWorkGroup = item?.Source;
		// Switching Work Group invalidates any prior Work pick and rebuilds the
		// Work list for the new group.
		_pendingWork = null;
		RebuildWorkItems();
		SyncListViewSelections();
		RefreshStepUi();
	}

	void OnWorkSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged)
			return;
		var item = WorkListView.SelectedItem as WorkListItem;
		_pendingWork = item?.Source;
		RefreshStepUi();
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
