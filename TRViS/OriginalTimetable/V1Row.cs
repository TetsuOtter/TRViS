// V1Row.cs — Programmatic row Views for OriginalTimetableV1Page.
//
// Replaces the inline <CollectionView><DataTemplate>...</DataTemplate></CollectionView>
// of the original XAML. CollectionView routes through MAUI's iOS UICollectionView
// handler → SwiftUI ViewGraph, which trips the ObservationTracking._AccessList /
// _NativeDictionary.copy use-after-free in iOS 18 / Swift 6 (swiftlang#84228) when
// the inner template contains an HtmlAutoDetectLabel (a Label subclass MAUI maps
// through a SwiftUI-backed Text). The crash is deterministic during cell recycling.
//
// Workaround: replace CollectionView with a `<VerticalStackLayout
// BindableLayout.ItemsSource=...>` inside a ScrollView. BindableLayout instantiates
// the template per item and adds the result as a regular Child of the parent
// layout — no UICollectionView, no SwiftUI ViewGraph crash path. Mirrors the
// `Grid.Children.Add()` pattern that the D-TAC `VerticalTimetableView` uses
// successfully on the same simulator.
//
// Two row classes for the two layouts:
//   • V1RowTablet   — 6-column row (run / station+badges / arrive / depart / track / limit)
//   • V1RowCompact  — 4-column row (station+badges / arrive / depart / track)
//
// Both wrap a `SwipeView` containing a `VerticalStackLayout` with three children:
//   1) Section-break Border (IsVisible bound to V1RowItem.IsSectionBreakRow)
//   2) Normal row Border (IsVisible bound to V1RowItem.IsNormalRow)
//   3) Inline note-fold Border (IsVisible bound to V1RowItem.IsNoteOpen, contains HtmlAutoDetectLabel)
//
// Page-scoped commands (OpenMarkerPopoverFromSwipeCommand, ToggleNoteCommand,
// OpenMemoCommand, ClearMarkerCommand) are invoked via a lazy parent-walk to
// `OriginalTimetableV1Page`. SwipeItems and the badge / note tap gestures
// resolve the page on first interaction (rows live well past page construction,
// so the walk always succeeds on tap).

using System.ComponentModel;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.DTAC;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

#region Tablet layout (≥ 600pt) — 6-column row

/// <summary>
/// Tablet-layout row View for the V1 Original Timetable page. Mirrors the
/// 6-column DataTemplate that previously lived inside <c>TabletRowsList</c>
/// (CollectionView). Subscribes to V1RowItem INPC events on
/// <c>OnBindingContextChanged</c> and pushes initial values into the
/// constructor-built tree; releases the subscription on the next context swap.
/// </summary>
public sealed class V1RowTablet : ContentView
{
	V1RowItem? _item;

	// Sub-views we mutate when V1RowItem properties change.
	readonly SwipeItem _swipeMarker;
	readonly SwipeItem _swipeMemo;
	readonly SwipeItem _swipeClear;

	readonly Border _sectionBreakBorder;
	readonly Label _sectionBreakLabel;

	readonly Border _normalRowBorder;
	readonly Label _runLabel;
	readonly BoxView _currentAccentBar;
	readonly HtmlAutoDetectLabel _stationNameLabel;
	readonly Border _markerBadgeBorder;
	readonly Label _markerBadgeLabel;
	readonly Ellipse _memoDot;
	readonly Border _noteToggleBorder;
	readonly Label _arriveLabel;
	readonly Label _departLabel;
	readonly Border _trackBorder;
	readonly HtmlAutoDetectLabel _trackLabel;
	readonly Label _limitLabel;

	readonly Border _noteBodyBorder;
	readonly HtmlAutoDetectLabel _noteBodyLabel;

	readonly Grid _innerRowGrid;

	public V1RowTablet()
	{
		// Resource lookups (top-level Application.Current.Resources). Falls back
		// to neutral grays so the row still renders if a resource key is absent
		// (defence in depth — should never happen with the standard Colors.xaml).
		var res = Application.Current?.Resources;
		Brush bgBrush       = (Brush?)res?["OT_Bg"]        ?? new SolidColorBrush(Colors.White);
		Brush bgSoftBrush   = (Brush?)res?["OT_BgSoft"]    ?? new SolidColorBrush(Colors.LightGray);
		Brush ruleBrush     = (Brush?)res?["OT_Rule"]      ?? new SolidColorBrush(Colors.Gray);
		Brush rowAltBrush   = (Brush?)res?["OT_RowAlt"]    ?? new SolidColorBrush(Colors.WhiteSmoke);
		Brush rowCurBrush   = (Brush?)res?["OT_RowCurrent"]?? new SolidColorBrush(Colors.LightYellow);
		Brush platBgBrush   = (Brush?)res?["OT_PlatBg"]    ?? new SolidColorBrush(Colors.LightGray);

		// ── SwipeItems ────────────────────────────────────────────────────────
		_swipeMarker = new SwipeItem
		{
			Text = "マーカー",
			BackgroundColor = LookupColor("OT_Accent_Light"),
		};
		_swipeMarker.Invoked += (_, _) => InvokeOnPage(p => p.OpenMarkerPopoverFromSwipe(_item?.Id));

		_swipeMemo = new SwipeItem
		{
			Text = "メモ",
			BackgroundColor = LookupColor("OT_MarkerCautionBg"),
		};
		_swipeMemo.Invoked += (_, _) => InvokeOnPage(p => p.OpenMemoFromRow(_item?.Id));

		_swipeClear = new SwipeItem
		{
			Text = "クリア",
			BackgroundColor = LookupColor("OT_AccentSoft_Light"),
		};
		_swipeClear.Invoked += (_, _) => InvokeOnPage(p => p.ClearMarkerFromRow(_item?.Id));

		// ── Section-break Border ──────────────────────────────────────────────
		_sectionBreakLabel = new Label
		{
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_sectionBreakBorder = new Border
		{
			Padding = new Thickness(14, 8),
			Background = bgSoftBrush,
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
			Content = _sectionBreakLabel,
			IsVisible = false,
		};

		// ── Normal-row Border ─────────────────────────────────────────────────
		_runLabel = new Label
		{
			FontSize = 16,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			FontFamily = "Menlo",
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_currentAccentBar = new BoxView
		{
			WidthRequest = 6,
			HeightRequest = 22,
			Color = LookupColorThemeAware("OT_Accent_Light", "OT_Accent_Dark"),
			VerticalOptions = LayoutOptions.Center,
			IsVisible = false,
		};
		_stationNameLabel = new HtmlAutoDetectLabel
		{
			FontSize = 32,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};

		// Marker badge — tap opens MarkerPopover anchored to this Border.
		_markerBadgeLabel = new Label
		{
			FontSize = 11,
			FontAttributes = FontAttributes.Bold,
			TextColor = LookupColor("OT_MarkerFlagFg"),
		};
		_markerBadgeBorder = new Border
		{
			Padding = new Thickness(6, 1),
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
			VerticalOptions = LayoutOptions.Center,
			Background = bgBrush,
			Content = _markerBadgeLabel,
			IsVisible = false,
		};
		var markerTap = new TapGestureRecognizer();
		markerTap.Tapped += (s, _) => InvokeOnPage(p => p.OpenMarkerPopoverFromBadge(s as View, _item?.Id));
		_markerBadgeBorder.GestureRecognizers.Add(markerTap);

		_memoDot = new Ellipse
		{
			WidthRequest = 8,
			HeightRequest = 8,
			Fill = (Brush?)res?["OT_AccentFgStrong"]
				?? new SolidColorBrush(LookupColor("OT_AccentFgStrong_Light")),
			VerticalOptions = LayoutOptions.Center,
			IsVisible = false,
		};

		_noteToggleBorder = new Border
		{
			WidthRequest = 24,
			HeightRequest = 24,
			StrokeThickness = 0.5,
			Stroke = ruleBrush,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
			Background = bgSoftBrush,
			VerticalOptions = LayoutOptions.Center,
			Content = new Label
			{
				Text = "≡", // ≡
				FontSize = 14,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
				TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
			},
			IsVisible = false,
		};
		var noteToggleTap = new TapGestureRecognizer();
		noteToggleTap.Tapped += (_, _) => InvokeOnPage(p => p.ToggleNoteForRow(_item?.Id));
		_noteToggleBorder.GestureRecognizers.Add(noteToggleTap);

		var stationCluster = new HorizontalStackLayout
		{
			Spacing = 6,
			VerticalOptions = LayoutOptions.Center,
		};
		stationCluster.Children.Add(_currentAccentBar);
		stationCluster.Children.Add(_stationNameLabel);
		stationCluster.Children.Add(_markerBadgeBorder);
		stationCluster.Children.Add(_memoDot);
		stationCluster.Children.Add(_noteToggleBorder);

		_arriveLabel = new Label
		{
			FontSize = 28,
			FontFamily = "Menlo",
			LineBreakMode = LineBreakMode.NoWrap,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_departLabel = new Label
		{
			FontSize = 28,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			LineBreakMode = LineBreakMode.NoWrap,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_trackLabel = new HtmlAutoDetectLabel
		{
			FontSize = 22,
			FontAttributes = FontAttributes.Bold,
			TextColor = LookupColorThemeAware("OT_PlatFg_Light", "OT_PlatFg_Dark"),
		};
		_trackBorder = new Border
		{
			Background = platBgBrush,
			Padding = new Thickness(6, 2),
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Content = _trackLabel,
			IsVisible = false,
		};
		_limitLabel = new Label
		{
			FontSize = 16,
			FontFamily = "Menlo",
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};

		_innerRowGrid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(new GridLength(40)),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(new GridLength(100)),
				new ColumnDefinition(new GridLength(100)),
				new ColumnDefinition(new GridLength(64)),
				new ColumnDefinition(new GridLength(40)),
			},
			ColumnSpacing = 4,
			RowSpacing = 0,
			Padding = new Thickness(12, 8),
		};
		Grid.SetColumn(_runLabel, 0);            _innerRowGrid.Children.Add(_runLabel);
		Grid.SetColumn(stationCluster, 1);       _innerRowGrid.Children.Add(stationCluster);
		Grid.SetColumn(_arriveLabel, 2);         _innerRowGrid.Children.Add(_arriveLabel);
		Grid.SetColumn(_departLabel, 3);         _innerRowGrid.Children.Add(_departLabel);
		Grid.SetColumn(_trackBorder, 4);         _innerRowGrid.Children.Add(_trackBorder);
		Grid.SetColumn(_limitLabel, 5);          _innerRowGrid.Children.Add(_limitLabel);

		// MAUI's Border.StrokeThickness is a single double (uniform around the
		// shape), not a per-side Thickness. The original XAML used
		// `StrokeThickness="0,0,0,1"` to get a bottom-only divider — XAML's
		// converter accepts that syntax, but it's not a valid C# value. We
		// emulate the bottom rule with a 1pt BoxView appended below the inner
		// grid; the Border itself keeps StrokeThickness=0.
		var bottomRule = new BoxView
		{
			HeightRequest = 1,
			Color = LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark"),
		};
		var normalRowStack = new VerticalStackLayout { Spacing = 0 };
		normalRowStack.Children.Add(_innerRowGrid);
		normalRowStack.Children.Add(bottomRule);
		_normalRowBorder = new Border
		{
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
			Padding = 0,
			Background = bgBrush,
			Content = normalRowStack,
			IsVisible = false,
		};

		// ── Inline note-fold Border (HtmlAutoDetectLabel — the previously-crashing
		// control. Safe inside BindableLayout-rendered tree because the parent is a
		// plain VerticalStackLayout, not a UICollectionView/ViewGraph). Same per-
		// side-stroke workaround as above: top + bottom rules emulated by BoxViews.
		_noteBodyLabel = new HtmlAutoDetectLabel
		{
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
			FontSize = 13,
		};
		var noteTopRule = new BoxView { HeightRequest = 1, Color = LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark") };
		var noteBottomRule = new BoxView { HeightRequest = 1, Color = LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark") };
		var noteBodyInner = new VerticalStackLayout { Spacing = 0 };
		noteBodyInner.Children.Add(noteTopRule);
		noteBodyInner.Children.Add(new Border
		{
			BackgroundColor = LookupColorThemeAware("OT_BgSoft_Light", "OT_BgSoft_Dark"),
			StrokeThickness = 0,
			Padding = new Thickness(14, 8),
			Content = _noteBodyLabel,
		});
		noteBodyInner.Children.Add(noteBottomRule);
		_noteBodyBorder = new Border
		{
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
			Padding = 0,
			Content = noteBodyInner,
			IsVisible = false,
		};

		// ── Compose ───────────────────────────────────────────────────────────
		var rowStack = new VerticalStackLayout { Spacing = 0 };
		rowStack.Children.Add(_sectionBreakBorder);
		rowStack.Children.Add(_normalRowBorder);
		rowStack.Children.Add(_noteBodyBorder);

		var swipe = new SwipeView
		{
			RightItems = new SwipeItems(new[] { _swipeMarker, _swipeMemo, _swipeClear })
			{
				Mode = SwipeMode.Reveal,
			},
			Content = rowStack,
		};

		Content = swipe;
	}

	protected override void OnBindingContextChanged()
	{
		base.OnBindingContextChanged();

		if (_item is not null)
		{
			_item.PropertyChanged -= OnItemPropertyChanged;
			_item = null;
		}

		if (BindingContext is V1RowItem next)
		{
			_item = next;
			_item.PropertyChanged += OnItemPropertyChanged;
			ApplyAll();
		}
	}

	void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// Targeted updates for the props that V1Page mutates in-place.
		switch (e.PropertyName)
		{
			case nameof(V1RowItem.IsCurrent):
				ApplyCurrent();
				break;
			case nameof(V1RowItem.IsNoteOpen):
				ApplyNoteOpen();
				break;
			case nameof(V1RowItem.HasMemo):
				_memoDot.IsVisible = _item!.HasMemo;
				break;
			case nameof(V1RowItem.Marker):
			case nameof(V1RowItem.HasMarker):
			case nameof(V1RowItem.MarkerText):
			case nameof(V1RowItem.IsMarkerFlag):
			case nameof(V1RowItem.IsMarkerCaution):
			case nameof(V1RowItem.IsMarkerStar):
				ApplyMarker();
				break;
			case nameof(V1RowItem.TabletRowPadding):
				_innerRowGrid.Padding = _item!.TabletRowPadding;
				break;
			case nameof(V1RowItem.TabletStationFontSize):
				_stationNameLabel.FontSize = _item!.TabletStationFontSize;
				break;
		}
	}

	void ApplyAll()
	{
		if (_item is null)
			return;
		AutomationId = _item.RowAutomationId;
		_swipeMarker.AutomationId = _item.MarkerAutomationId;
		_swipeMemo.AutomationId   = _item.MemoAutomationId;
		_swipeClear.AutomationId  = _item.ClearAutomationId;
		_markerBadgeBorder.AutomationId = _item.MarkerBadgeAutomationId;
		_noteBodyBorder.AutomationId    = _item.NoteBodyAutomationId;

		// Section break vs normal row visibility.
		_sectionBreakBorder.IsVisible = _item.IsSectionBreakRow;
		_normalRowBorder.IsVisible    = !_item.IsSectionBreakRow;

		_sectionBreakLabel.Text = _item.SectionBreakLabel;

		// Static text content (set once per Items rebuild; subsequent in-place
		// updates target the props enumerated in OnItemPropertyChanged).
		_runLabel.Text     = _item.RunText;
		_stationNameLabel.Text = _item.StationName;
		_stationNameLabel.FontSize = _item.TabletStationFontSize;
		_arriveLabel.Text  = _item.ArriveText;
		_departLabel.Text  = _item.DepartText;
		// Track tile holds 2 full-width chars at base size; for 3+ chars shrink
		// the font (same rule as DTAC) so it stays within the 2-char-wide tile.
		_trackLabel.FontSize = string.IsNullOrEmpty(_item.TrackName)
			? 22
			: DTACElementStyles.GetTimetableTrackLabelFontSize(_item.TrackName, 22);
		_trackLabel.Text   = _item.TrackName;
		_trackBorder.IsVisible = !string.IsNullOrEmpty(_item.TrackName);
		_limitLabel.Text   = _item.LimitText;

		// Pass-row dimming (station name only). Sticks for the lifetime of this
		// V1RowItem instance — IsPass is not mutated in-place.
		_stationNameLabel.TextColor = _item.IsPass
			? LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark")
			: LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark");

		_innerRowGrid.Padding = _item.TabletRowPadding;

		// Note-toggle button visible only when row has a remarks body.
		_noteToggleBorder.IsVisible = _item.HasNote;
		_noteBodyLabel.Text = _item.NoteText;

		ApplyCurrent();
		ApplyMarker();
		ApplyNoteOpen();
		_memoDot.IsVisible = _item.HasMemo;
	}

	void ApplyCurrent()
	{
		if (_item is null)
			return;
		_currentAccentBar.IsVisible = _item.IsCurrent;

		// Row background trigger: alt → OT_RowAlt, current → OT_RowCurrent, else OT_Bg.
		var res = Application.Current?.Resources;
		Brush bg;
		if (_item.IsCurrent)
			bg = (Brush?)res?["OT_RowCurrent"] ?? new SolidColorBrush(Colors.LightYellow);
		else if (_item.IsAlternateRow)
			bg = (Brush?)res?["OT_RowAlt"] ?? new SolidColorBrush(Colors.WhiteSmoke);
		else
			bg = (Brush?)res?["OT_Bg"] ?? new SolidColorBrush(Colors.White);
		_normalRowBorder.Background = bg;
	}

	void ApplyMarker()
	{
		if (_item is null)
			return;
		_markerBadgeBorder.IsVisible = _item.HasMarker;
		_markerBadgeLabel.Text = _item.MarkerText;

		// Background/foreground per marker kind. Brush keys defined in Colors.xaml.
		var res = Application.Current?.Resources;
		if (_item.IsMarkerFlag)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerFlagBgBrush"]
				?? new SolidColorBrush(LookupColor("OT_MarkerFlagBg"));
			_markerBadgeLabel.TextColor = LookupColor("OT_MarkerFlagFg");
		}
		else if (_item.IsMarkerCaution)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerCautionBgBrush"]
				?? new SolidColorBrush(LookupColor("OT_MarkerCautionBg"));
			_markerBadgeLabel.TextColor = LookupColor("OT_MarkerCautionFg");
		}
		else if (_item.IsMarkerStar)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerStarBgBrush"]
				?? new SolidColorBrush(LookupColor("OT_MarkerStarBg"));
			_markerBadgeLabel.TextColor = LookupColor("OT_MarkerStarFg");
		}
		else
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_Bg"]
				?? new SolidColorBrush(Colors.White);
			_markerBadgeLabel.TextColor = LookupColor("OT_MarkerFlagFg");
		}
	}

	void ApplyNoteOpen()
	{
		if (_item is null)
			return;
		_noteBodyBorder.IsVisible = _item.IsNoteOpen;
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	static Color LookupColor(string key)
		=> (Application.Current?.Resources[key] as Color) ?? Colors.Black;

	static Color LookupColorThemeAware(string lightKey, string darkKey)
	{
		// Prefer the AppTheme-driven Light/Dark resource pair. Layout-time only —
		// we don't react to runtime theme flips inside rows (rare; the page
		// reloads on theme change anyway).
		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		return LookupColor(isDark ? darkKey : lightKey);
	}

	void InvokeOnPage(Action<OriginalTimetableV1Page> action)
	{
		// Walk up the visual tree to find the host V1 page. Rows are children
		// of a VerticalStackLayout inside a ScrollView inside the V1Page; the
		// walk takes ≤6 hops on a healthy tree.
		Element? cur = this;
		while (cur is not null)
		{
			if (cur is OriginalTimetableV1Page page)
			{
				action(page);
				return;
			}
			cur = cur.Parent;
		}
		// Swallow — if the row is detached (post-rebuild race) the user action
		// is a no-op rather than a crash.
	}
}

#endregion

#region Compact layout (< 600pt) — 4-column row

/// <summary>
/// Compact-layout row View. Mirrors the 4-column compact DataTemplate that
/// previously lived inside <c>CompactRowsList</c> (CollectionView). Identical
/// INPC subscription pattern to <see cref="V1RowTablet"/>; cells differ in
/// font sizes, column count, and the dropped Run / Limit columns.
/// </summary>
public sealed class V1RowCompact : ContentView
{
	V1RowItem? _item;

	readonly SwipeItem _swipeMarker;
	readonly SwipeItem _swipeMemo;
	readonly SwipeItem _swipeClear;

	readonly Border _sectionBreakBorder;
	readonly Label _sectionBreakLabel;

	readonly Border _normalRowBorder;
	readonly BoxView _currentAccentBar;
	readonly HtmlAutoDetectLabel _stationNameLabel;
	readonly Border _markerBadgeBorder;
	readonly Label _markerBadgeLabel;
	readonly Ellipse _memoDot;
	readonly Border _noteToggleBorder;
	readonly Label _arriveLabel;
	readonly Label _departLabel;
	readonly Border _trackBorder;
	readonly HtmlAutoDetectLabel _trackLabel;

	readonly Border _noteBodyBorder;
	readonly HtmlAutoDetectLabel _noteBodyLabel;

	readonly Grid _innerRowGrid;

	public V1RowCompact()
	{
		var res = Application.Current?.Resources;
		Brush bgBrush       = (Brush?)res?["OT_Bg"]        ?? new SolidColorBrush(Colors.White);
		Brush bgSoftBrush   = (Brush?)res?["OT_BgSoft"]    ?? new SolidColorBrush(Colors.LightGray);
		Brush ruleBrush     = (Brush?)res?["OT_Rule"]      ?? new SolidColorBrush(Colors.Gray);
		Brush platBgBrush   = (Brush?)res?["OT_PlatBg"]    ?? new SolidColorBrush(Colors.LightGray);

		_swipeMarker = new SwipeItem { Text = "マーカー", BackgroundColor = V1RowTablet_LookupColor("OT_Accent_Light") };
		_swipeMarker.Invoked += (_, _) => InvokeOnPage(p => p.OpenMarkerPopoverFromSwipe(_item?.Id));
		_swipeMemo = new SwipeItem { Text = "メモ", BackgroundColor = V1RowTablet_LookupColor("OT_MarkerCautionBg") };
		_swipeMemo.Invoked += (_, _) => InvokeOnPage(p => p.OpenMemoFromRow(_item?.Id));
		_swipeClear = new SwipeItem { Text = "クリア", BackgroundColor = V1RowTablet_LookupColor("OT_AccentSoft_Light") };
		_swipeClear.Invoked += (_, _) => InvokeOnPage(p => p.ClearMarkerFromRow(_item?.Id));

		_sectionBreakLabel = new Label
		{
			FontSize = 11,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V1RowTablet_LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_sectionBreakBorder = new Border
		{
			Padding = new Thickness(12, 6),
			Background = bgSoftBrush,
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
			Content = _sectionBreakLabel,
			IsVisible = false,
		};

		_currentAccentBar = new BoxView
		{
			WidthRequest = 4,
			HeightRequest = 16,
			Color = V1RowTablet_LookupColorThemeAware("OT_Accent_Light", "OT_Accent_Dark"),
			VerticalOptions = LayoutOptions.Center,
			IsVisible = false,
		};
		_stationNameLabel = new HtmlAutoDetectLabel
		{
			FontSize = 20,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V1RowTablet_LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_markerBadgeLabel = new Label
		{
			FontSize = 10,
			FontAttributes = FontAttributes.Bold,
			TextColor = V1RowTablet_LookupColor("OT_MarkerFlagFg"),
		};
		_markerBadgeBorder = new Border
		{
			Padding = new Thickness(5, 1),
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
			VerticalOptions = LayoutOptions.Center,
			Background = bgBrush,
			Content = _markerBadgeLabel,
			IsVisible = false,
		};
		var markerTap = new TapGestureRecognizer();
		markerTap.Tapped += (s, _) => InvokeOnPage(p => p.OpenMarkerPopoverFromBadge(s as View, _item?.Id));
		_markerBadgeBorder.GestureRecognizers.Add(markerTap);

		_memoDot = new Ellipse
		{
			WidthRequest = 7,
			HeightRequest = 7,
			Fill = (Brush?)res?["OT_AccentFgStrong"]
				?? new SolidColorBrush(V1RowTablet_LookupColor("OT_AccentFgStrong_Light")),
			VerticalOptions = LayoutOptions.Center,
			IsVisible = false,
		};
		_noteToggleBorder = new Border
		{
			WidthRequest = 22,
			HeightRequest = 22,
			StrokeThickness = 0.5,
			Stroke = ruleBrush,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 11 },
			Background = bgSoftBrush,
			VerticalOptions = LayoutOptions.Center,
			Content = new Label
			{
				Text = "≡",
				FontSize = 12,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
				TextColor = V1RowTablet_LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
			},
			IsVisible = false,
		};
		var noteToggleTap = new TapGestureRecognizer();
		noteToggleTap.Tapped += (_, _) => InvokeOnPage(p => p.ToggleNoteForRow(_item?.Id));
		_noteToggleBorder.GestureRecognizers.Add(noteToggleTap);

		var stationCluster = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
		stationCluster.Children.Add(_currentAccentBar);
		stationCluster.Children.Add(_stationNameLabel);
		stationCluster.Children.Add(_markerBadgeBorder);
		stationCluster.Children.Add(_memoDot);
		stationCluster.Children.Add(_noteToggleBorder);

		_arriveLabel = new Label
		{
			FontSize = 18,
			FontFamily = "Menlo",
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			TextColor = V1RowTablet_LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_departLabel = new Label
		{
			FontSize = 18,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			TextColor = V1RowTablet_LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_trackLabel = new HtmlAutoDetectLabel
		{
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			TextColor = V1RowTablet_LookupColorThemeAware("OT_PlatFg_Light", "OT_PlatFg_Dark"),
		};
		_trackBorder = new Border
		{
			Background = platBgBrush,
			Padding = new Thickness(5, 2),
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Content = _trackLabel,
			IsVisible = false,
		};

		_innerRowGrid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(new GridLength(70)),
				new ColumnDefinition(new GridLength(70)),
				new ColumnDefinition(new GridLength(52)),
			},
			ColumnSpacing = 4,
			RowSpacing = 0,
			Padding = new Thickness(10, 7),
		};
		Grid.SetColumn(stationCluster, 0); _innerRowGrid.Children.Add(stationCluster);
		Grid.SetColumn(_arriveLabel, 1);   _innerRowGrid.Children.Add(_arriveLabel);
		Grid.SetColumn(_departLabel, 2);   _innerRowGrid.Children.Add(_departLabel);
		Grid.SetColumn(_trackBorder, 3);   _innerRowGrid.Children.Add(_trackBorder);

		// Same per-side-stroke emulation as V1RowTablet: MAUI Border.StrokeThickness
		// is a uniform double; the original XAML's "0,0,0,1" syntax can't be
		// expressed in C#. Append a 1pt BoxView divider below the inner grid.
		var bottomRule = new BoxView { HeightRequest = 1, Color = V1RowTablet_LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark") };
		var normalRowStack = new VerticalStackLayout { Spacing = 0 };
		normalRowStack.Children.Add(_innerRowGrid);
		normalRowStack.Children.Add(bottomRule);
		_normalRowBorder = new Border
		{
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
			Padding = 0,
			Background = bgBrush,
			Content = normalRowStack,
			IsVisible = false,
		};

		_noteBodyLabel = new HtmlAutoDetectLabel
		{
			TextColor = V1RowTablet_LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
			FontSize = 12,
		};
		var noteTopRule = new BoxView { HeightRequest = 1, Color = V1RowTablet_LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark") };
		var noteBottomRule = new BoxView { HeightRequest = 1, Color = V1RowTablet_LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark") };
		var noteBodyInner = new VerticalStackLayout { Spacing = 0 };
		noteBodyInner.Children.Add(noteTopRule);
		noteBodyInner.Children.Add(new Border
		{
			BackgroundColor = V1RowTablet_LookupColorThemeAware("OT_BgSoft_Light", "OT_BgSoft_Dark"),
			StrokeThickness = 0,
			Padding = new Thickness(12, 6),
			Content = _noteBodyLabel,
		});
		noteBodyInner.Children.Add(noteBottomRule);
		_noteBodyBorder = new Border
		{
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
			Padding = 0,
			Content = noteBodyInner,
			IsVisible = false,
		};

		var rowStack = new VerticalStackLayout { Spacing = 0 };
		rowStack.Children.Add(_sectionBreakBorder);
		rowStack.Children.Add(_normalRowBorder);
		rowStack.Children.Add(_noteBodyBorder);

		var swipe = new SwipeView
		{
			RightItems = new SwipeItems(new[] { _swipeMarker, _swipeMemo, _swipeClear }) { Mode = SwipeMode.Reveal },
			Content = rowStack,
		};

		Content = swipe;
	}

	protected override void OnBindingContextChanged()
	{
		base.OnBindingContextChanged();

		if (_item is not null)
		{
			_item.PropertyChanged -= OnItemPropertyChanged;
			_item = null;
		}

		if (BindingContext is V1RowItem next)
		{
			_item = next;
			_item.PropertyChanged += OnItemPropertyChanged;
			ApplyAll();
		}
	}

	void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(V1RowItem.IsCurrent):
				ApplyCurrent();
				break;
			case nameof(V1RowItem.IsNoteOpen):
				ApplyNoteOpen();
				break;
			case nameof(V1RowItem.HasMemo):
				_memoDot.IsVisible = _item!.HasMemo;
				break;
			case nameof(V1RowItem.Marker):
			case nameof(V1RowItem.HasMarker):
			case nameof(V1RowItem.MarkerText):
			case nameof(V1RowItem.IsMarkerFlag):
			case nameof(V1RowItem.IsMarkerCaution):
			case nameof(V1RowItem.IsMarkerStar):
				ApplyMarker();
				break;
			case nameof(V1RowItem.CompactRowPadding):
				_innerRowGrid.Padding = _item!.CompactRowPadding;
				break;
			case nameof(V1RowItem.CompactStationFontSize):
				_stationNameLabel.FontSize = _item!.CompactStationFontSize;
				break;
		}
	}

	void ApplyAll()
	{
		if (_item is null)
			return;
		AutomationId = _item.RowAutomationId;
		_swipeMarker.AutomationId = _item.MarkerAutomationId;
		_swipeMemo.AutomationId   = _item.MemoAutomationId;
		_swipeClear.AutomationId  = _item.ClearAutomationId;
		_markerBadgeBorder.AutomationId = _item.MarkerBadgeAutomationId;
		_noteBodyBorder.AutomationId    = _item.NoteBodyAutomationId;

		_sectionBreakBorder.IsVisible = _item.IsSectionBreakRow;
		_normalRowBorder.IsVisible    = !_item.IsSectionBreakRow;

		_sectionBreakLabel.Text = _item.SectionBreakLabel;

		_stationNameLabel.Text = _item.StationName;
		_stationNameLabel.FontSize = _item.CompactStationFontSize;
		_arriveLabel.Text  = _item.ArriveText;
		_departLabel.Text  = _item.DepartText;
		// Track tile holds 2 full-width chars at base size; for 3+ chars shrink
		// the font (same rule as DTAC) so it stays within the 2-char-wide tile.
		_trackLabel.FontSize = string.IsNullOrEmpty(_item.TrackName)
			? 16
			: DTACElementStyles.GetTimetableTrackLabelFontSize(_item.TrackName, 16);
		_trackLabel.Text   = _item.TrackName;
		_trackBorder.IsVisible = !string.IsNullOrEmpty(_item.TrackName);

		_stationNameLabel.TextColor = _item.IsPass
			? V1RowTablet_LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark")
			: V1RowTablet_LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark");

		_innerRowGrid.Padding = _item.CompactRowPadding;

		_noteToggleBorder.IsVisible = _item.HasNote;
		_noteBodyLabel.Text = _item.NoteText;

		ApplyCurrent();
		ApplyMarker();
		ApplyNoteOpen();
		_memoDot.IsVisible = _item.HasMemo;
	}

	void ApplyCurrent()
	{
		if (_item is null)
			return;
		_currentAccentBar.IsVisible = _item.IsCurrent;
		var res = Application.Current?.Resources;
		Brush bg;
		if (_item.IsCurrent)
			bg = (Brush?)res?["OT_RowCurrent"] ?? new SolidColorBrush(Colors.LightYellow);
		else if (_item.IsAlternateRow)
			bg = (Brush?)res?["OT_RowAlt"] ?? new SolidColorBrush(Colors.WhiteSmoke);
		else
			bg = (Brush?)res?["OT_Bg"] ?? new SolidColorBrush(Colors.White);
		_normalRowBorder.Background = bg;
	}

	void ApplyMarker()
	{
		if (_item is null)
			return;
		_markerBadgeBorder.IsVisible = _item.HasMarker;
		_markerBadgeLabel.Text = _item.MarkerText;

		var res = Application.Current?.Resources;
		if (_item.IsMarkerFlag)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerFlagBgBrush"]
				?? new SolidColorBrush(V1RowTablet_LookupColor("OT_MarkerFlagBg"));
			_markerBadgeLabel.TextColor = V1RowTablet_LookupColor("OT_MarkerFlagFg");
		}
		else if (_item.IsMarkerCaution)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerCautionBgBrush"]
				?? new SolidColorBrush(V1RowTablet_LookupColor("OT_MarkerCautionBg"));
			_markerBadgeLabel.TextColor = V1RowTablet_LookupColor("OT_MarkerCautionFg");
		}
		else if (_item.IsMarkerStar)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerStarBgBrush"]
				?? new SolidColorBrush(V1RowTablet_LookupColor("OT_MarkerStarBg"));
			_markerBadgeLabel.TextColor = V1RowTablet_LookupColor("OT_MarkerStarFg");
		}
		else
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_Bg"]
				?? new SolidColorBrush(Colors.White);
			_markerBadgeLabel.TextColor = V1RowTablet_LookupColor("OT_MarkerFlagFg");
		}
	}

	void ApplyNoteOpen()
	{
		if (_item is null)
			return;
		_noteBodyBorder.IsVisible = _item.IsNoteOpen;
	}

	// Re-use the static helpers from V1RowTablet to avoid duplication. Methods
	// are declared internal-static via the named exports below.
	static Color V1RowTablet_LookupColor(string key)
		=> (Application.Current?.Resources[key] as Color) ?? Colors.Black;

	static Color V1RowTablet_LookupColorThemeAware(string lightKey, string darkKey)
	{
		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		return V1RowTablet_LookupColor(isDark ? darkKey : lightKey);
	}

	void InvokeOnPage(Action<OriginalTimetableV1Page> action)
	{
		Element? cur = this;
		while (cur is not null)
		{
			if (cur is OriginalTimetableV1Page page)
			{
				action(page);
				return;
			}
			cur = cur.Parent;
		}
	}
}

#endregion
