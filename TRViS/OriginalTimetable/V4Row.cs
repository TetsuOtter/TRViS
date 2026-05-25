// V4Row.cs — Programmatic row Views for OriginalTimetableV4Page (Next Big).
//
// Mirrors the V1Row.cs / V2Row.cs workaround for the Apple Swift
// ObservationTracking `_AccessList` / `_NativeDictionary.copy` use-after-free
// (swiftlang#84228): the V4 XAML mini-list used to wrap a `SwipeView` +
// `HtmlAutoDetectLabel` inside `<CollectionView>` + `<DataTemplate>`, which
// routes through MAUI's iOS UICollectionView handler → SwiftUI ViewGraph and
// trips the crash during cell recycling. Replacing the CollectionView with
// BindableLayout-on-VSL keeps the visual identical but emits plain Element
// siblings — no ViewGraph path.
//
// Two row classes match V1 / V2's tablet/compact split:
//   • V4RowTablet   — 4-column mini row (station+badges / arrive / depart / track)
//   • V4RowCompact  — same shape at smaller scale
//
// V4 differs from V2 in three ways worth flagging:
//   1) No "current emphasis" on the rows — current station lives in the hero
//      block above the mini list, so there is no `ApplyCurrent()` here. The
//      only per-current effect on rows is IsHiddenInList (the next-station
//      row hides because the hero shows it) and IsPassed (origIdx<curOrigIdx
//      fades the row to Opacity 0.4).
//   2) Flush-edge rows, not floating tiles. Outer Border is a Rectangle with
//      a 1pt bottom rule (no rounded corners, no margin, no card background
//      flip). No TabletCardMargin / CompactCardMargin properties exist on
//      V4RowItem — V4 reflows density via RowPadding instead.
//   3) Rows can be entirely hidden by `IsHiddenInList` (the row that the hero
//      currently advertises). V1/V2 have no analog. The programmatic row
//      subscribes to `IsHiddenInList` PropertyChanged to flip the outer
//      Border.IsVisible.
//
// SwipeItems use `.Invoked` + a Parent-walk to the page (same pattern as
// V1/V2), because Command bindings inside a programmatic SwipeItem can't
// resolve `Source={x:Reference Self}` against the XAML root.

using System.ComponentModel;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

#region Tablet layout (≥ 600pt)

/// <summary>
/// Tablet-layout mini row View for the V4 page. Mirrors the 4-column row that
/// previously lived inside <c>TabletMiniList</c> (CollectionView). Subscribes
/// to V4RowItem INPC on <c>OnBindingContextChanged</c>; releases on context
/// swap.
/// </summary>
public sealed class V4RowTablet : ContentView
{
	V4RowItem? _item;

	// Sub-views we mutate when V4RowItem properties change.
	readonly SwipeItem _swipeMarker;
	readonly SwipeItem _swipeMemo;
	readonly SwipeItem _swipeClear;

	readonly Border _sectionBreakBorder;
	readonly Label _sectionBreakLabel;

	readonly Border _normalRowBorder;
	readonly Grid _rowGrid;
	readonly Label _stationNameLabel;
	readonly Border _passChipBorder;
	readonly Border _markerBadgeBorder;
	readonly Label _markerBadgeLabel;
	readonly Ellipse _memoDot;
	readonly Border _noteToggleBorder;
	readonly Label _arriveLabel;
	readonly Label _departLabel;
	readonly Border _trackBorder;
	readonly Label _trackLabel;

	readonly Border _noteBodyBorder;
	readonly HtmlAutoDetectLabel _noteBodyLabel;

	public V4RowTablet()
	{
		var res = Application.Current?.Resources;
		Brush bgBrush       = (Brush?)res?["OT_Bg"]          ?? new SolidColorBrush(Colors.White);
		Brush bgSoftBrush   = (Brush?)res?["OT_BgSoft"]      ?? new SolidColorBrush(Colors.LightGray);
		Brush ruleBrush     = (Brush?)res?["OT_Rule"]        ?? new SolidColorBrush(Colors.Gray);
		Brush accentBrush   = (Brush?)res?["OT_Accent"]      ?? new SolidColorBrush(LookupColor("OT_Accent_Light"));
		Brush accentSoftBrush = (Brush?)res?["OT_AccentSoft"] ?? new SolidColorBrush(LookupColor("OT_AccentSoft_Light"));
		Brush platBgBrush   = (Brush?)res?["OT_PlatBg"]      ?? new SolidColorBrush(Colors.LightGray);

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

		// ── Section-break row (matches XAML: top+bottom rule, accent soft bg) ─
		_sectionBreakLabel = new Label
		{
			FontSize = 15,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_AccentFgStrong_Light", "OT_AccentFgStrong_Dark"),
		};
		// Section-break: outer Border carries the accent-soft fill + a 1pt
		// stroke (uniform — Border.StrokeThickness is double, not Thickness,
		// so we can't replicate the XAML's sided "0,1,0,0.5" exactly; a
		// uniform 1pt accent stroke is visually adjacent).
		_sectionBreakBorder = new Border
		{
			Padding = new Thickness(14, 8),
			Margin = new Thickness(0, 6, 0, 0),
			Background = accentSoftBrush,
			Stroke = accentBrush,
			StrokeThickness = 1,
			StrokeShape = new Rectangle(),
			Content = _sectionBreakLabel,
			IsVisible = false,
		};

		// ── Station cluster (left col) ────────────────────────────────────────
		_stationNameLabel = new Label
		{
			FontSize = 22,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};

		// Pass chip (通過)
		_passChipBorder = new Border
		{
			Background = ruleBrush,
			Padding = new Thickness(6, 1),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 3 },
			VerticalOptions = LayoutOptions.Center,
			Content = new Label
			{
				Text = "通過",
				FontSize = 11,
				TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
			},
			IsVisible = false,
		};

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
			StrokeShape = new RoundRectangle { CornerRadius = 4 },
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
			StrokeShape = new RoundRectangle { CornerRadius = 12 },
			Background = bgSoftBrush,
			VerticalOptions = LayoutOptions.Center,
			Content = new Label
			{
				Text = "≡",
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
		stationCluster.Children.Add(_stationNameLabel);
		stationCluster.Children.Add(_passChipBorder);
		stationCluster.Children.Add(_markerBadgeBorder);
		stationCluster.Children.Add(_memoDot);
		stationCluster.Children.Add(_noteToggleBorder);

		// ── Arrive / Depart labels ────────────────────────────────────────────
		_arriveLabel = new Label
		{
			FontSize = 17,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_departLabel = new Label
		{
			FontSize = 17,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};

		// ── Track chip ────────────────────────────────────────────────────────
		_trackLabel = new Label
		{
			Text = string.Empty,
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			TextColor = LookupColorThemeAware("OT_PlatFg_Light", "OT_PlatFg_Dark"),
		};
		_trackBorder = new Border
		{
			Background = platBgBrush,
			Padding = new Thickness(6, 2),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 4 },
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			Content = _trackLabel,
			IsVisible = false,
		};

		// ── Inner grid (4 cols: *,100,100,50) ────────────────────────────────
		_rowGrid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(new GridLength(100)),
				new ColumnDefinition(new GridLength(100)),
				new ColumnDefinition(new GridLength(50)),
			},
			ColumnSpacing = 8,
			Padding = new Thickness(14, 8),
		};
		Grid.SetColumn(stationCluster, 0); _rowGrid.Children.Add(stationCluster);
		Grid.SetColumn(_arriveLabel, 1);   _rowGrid.Children.Add(_arriveLabel);
		Grid.SetColumn(_departLabel, 2);   _rowGrid.Children.Add(_departLabel);
		Grid.SetColumn(_trackBorder, 3);   _rowGrid.Children.Add(_trackBorder);

		// ── Inline NoteFold (HtmlAutoDetectLabel — safe under BindableLayout) ─
		_noteBodyLabel = new HtmlAutoDetectLabel
		{
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
			FontSize = 12,
		};
		_noteBodyBorder = new Border
		{
			Margin = new Thickness(14, 0, 14, 8),
			BackgroundColor = LookupColorThemeAware("OT_BgSoft_Light", "OT_BgSoft_Dark"),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			Padding = new Thickness(10, 6),
			Content = _noteBodyLabel,
			IsVisible = false,
		};

		// Bottom rule (1pt) — replaces the XAML's sided StrokeThickness
		// "0,0,0,1" (Border.StrokeThickness is double, not Thickness; we can't
		// express "bottom-only" stroke programmatically, so render the rule
		// as a thin BoxView sibling at the bottom of the row inner stack).
		var bottomRule = new BoxView
		{
			HeightRequest = 1,
			Color = LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark"),
		};

		var rowInner = new VerticalStackLayout { Spacing = 0 };
		rowInner.Children.Add(_rowGrid);
		rowInner.Children.Add(_noteBodyBorder);
		rowInner.Children.Add(bottomRule);

		// Outer flush-edge row Border (no stroke; bottom rule is the BoxView
		// above). Keeps the background fill + lets us bind IsVisible per row
		// and Opacity per IsPassed.
		_normalRowBorder = new Border
		{
			Background = bgBrush,
			StrokeThickness = 0,
			StrokeShape = new Rectangle(),
			Padding = new Thickness(0),
			Content = rowInner,
			IsVisible = false,
		};

		// ── Compose ──────────────────────────────────────────────────────────
		var rowStack = new VerticalStackLayout { Spacing = 0 };
		rowStack.Children.Add(_sectionBreakBorder);
		rowStack.Children.Add(_normalRowBorder);

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

		if (BindingContext is V4RowItem next)
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
			case nameof(V4RowItem.IsHiddenInList):
				ApplyVisibility();
				break;
			case nameof(V4RowItem.IsPassed):
				_normalRowBorder.Opacity = _item!.IsPassed ? 0.4 : 1.0;
				break;
			case nameof(V4RowItem.IsNoteOpen):
				_noteBodyBorder.IsVisible = _item!.IsNoteOpen;
				break;
			case nameof(V4RowItem.HasMemo):
				_memoDot.IsVisible = _item!.HasMemo;
				break;
			case nameof(V4RowItem.Marker):
			case nameof(V4RowItem.HasMarker):
			case nameof(V4RowItem.MarkerText):
			case nameof(V4RowItem.IsMarkerFlag):
			case nameof(V4RowItem.IsMarkerCaution):
			case nameof(V4RowItem.IsMarkerStar):
				ApplyMarker();
				break;
			case nameof(V4RowItem.TabletStationFontSize):
				_stationNameLabel.FontSize = _item!.TabletStationFontSize;
				break;
			case nameof(V4RowItem.TabletTimeFontSize):
				_arriveLabel.FontSize = _item!.TabletTimeFontSize;
				_departLabel.FontSize = _item.TabletTimeFontSize;
				break;
			case nameof(V4RowItem.TabletRowPadding):
				_rowGrid.Padding = _item!.TabletRowPadding;
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
		_sectionBreakLabel.Text       = _item.SectionBreakLabel;
		ApplyVisibility();

		_stationNameLabel.Text     = _item.StationName;
		_stationNameLabel.FontSize = _item.TabletStationFontSize;
		_stationNameLabel.TextColor = _item.IsPass
			? LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark")
			: LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark");
		_passChipBorder.IsVisible = _item.IsPass;

		_arriveLabel.Text       = _item.ArriveText;
		_arriveLabel.FontSize   = _item.TabletTimeFontSize;
		_departLabel.Text       = _item.DepartText;
		_departLabel.FontSize   = _item.TabletTimeFontSize;
		_rowGrid.Padding        = _item.TabletRowPadding;

		_trackLabel.Text       = _item.TrackName;
		_trackBorder.IsVisible = _item.HasTrackName;

		_noteToggleBorder.IsVisible = _item.HasNote;
		_noteBodyLabel.Text         = _item.NoteText;
		_noteBodyBorder.IsVisible   = _item.IsNoteOpen;

		_normalRowBorder.Opacity = _item.IsPassed ? 0.4 : 1.0;

		ApplyMarker();
		_memoDot.IsVisible = _item.HasMemo;
	}

	void ApplyVisibility()
	{
		if (_item is null)
			return;
		// Outer row visible only when this is a *normal* row AND it isn't the
		// row the hero advertises (IsHiddenInList).
		_normalRowBorder.IsVisible = _item.IsVisibleNormalRow;
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

	// ── Helpers ──────────────────────────────────────────────────────────────

	internal static Color LookupColor(string key)
		=> (Application.Current?.Resources[key] as Color) ?? Colors.Black;

	internal static Color LookupColorThemeAware(string lightKey, string darkKey)
	{
		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		return LookupColor(isDark ? darkKey : lightKey);
	}

	void InvokeOnPage(Action<OriginalTimetableV4Page> action)
	{
		Element? cur = this;
		while (cur is not null)
		{
			if (cur is OriginalTimetableV4Page page)
			{
				action(page);
				return;
			}
			cur = cur.Parent;
		}
		// Swallow — detached row → no-op.
	}
}

#endregion

#region Compact layout (< 600pt)

/// <summary>
/// Compact-layout mini row View. Same shape as <see cref="V4RowTablet"/> at
/// smaller scale (compact column widths *,64,64,40; compact font scales;
/// tighter padding). Subscribes to V4RowItem.Compact* properties for the
/// in-place density updates.
/// </summary>
public sealed class V4RowCompact : ContentView
{
	V4RowItem? _item;

	readonly SwipeItem _swipeMarker;
	readonly SwipeItem _swipeMemo;
	readonly SwipeItem _swipeClear;

	readonly Border _sectionBreakBorder;
	readonly Label _sectionBreakLabel;

	readonly Border _normalRowBorder;
	readonly Grid _rowGrid;
	readonly Label _stationNameLabel;
	readonly Border _passChipBorder;
	readonly Border _markerBadgeBorder;
	readonly Label _markerBadgeLabel;
	readonly Ellipse _memoDot;
	readonly Border _noteToggleBorder;
	readonly Label _arriveLabel;
	readonly Label _departLabel;
	readonly Border _trackBorder;
	readonly Label _trackLabel;

	readonly Border _noteBodyBorder;
	readonly HtmlAutoDetectLabel _noteBodyLabel;

	public V4RowCompact()
	{
		var res = Application.Current?.Resources;
		Brush bgBrush       = (Brush?)res?["OT_Bg"]          ?? new SolidColorBrush(Colors.White);
		Brush bgSoftBrush   = (Brush?)res?["OT_BgSoft"]      ?? new SolidColorBrush(Colors.LightGray);
		Brush ruleBrush     = (Brush?)res?["OT_Rule"]        ?? new SolidColorBrush(Colors.Gray);
		Brush accentBrush   = (Brush?)res?["OT_Accent"]      ?? new SolidColorBrush(V4RowTablet.LookupColor("OT_Accent_Light"));
		Brush accentSoftBrush = (Brush?)res?["OT_AccentSoft"] ?? new SolidColorBrush(V4RowTablet.LookupColor("OT_AccentSoft_Light"));
		Brush platBgBrush   = (Brush?)res?["OT_PlatBg"]      ?? new SolidColorBrush(Colors.LightGray);

		_swipeMarker = new SwipeItem
		{
			Text = "マーカー",
			BackgroundColor = V4RowTablet.LookupColor("OT_Accent_Light"),
		};
		_swipeMarker.Invoked += (_, _) => InvokeOnPage(p => p.OpenMarkerPopoverFromSwipe(_item?.Id));

		_swipeMemo = new SwipeItem
		{
			Text = "メモ",
			BackgroundColor = V4RowTablet.LookupColor("OT_MarkerCautionBg"),
		};
		_swipeMemo.Invoked += (_, _) => InvokeOnPage(p => p.OpenMemoFromRow(_item?.Id));

		_swipeClear = new SwipeItem
		{
			Text = "クリア",
			BackgroundColor = V4RowTablet.LookupColor("OT_AccentSoft_Light"),
		};
		_swipeClear.Invoked += (_, _) => InvokeOnPage(p => p.ClearMarkerFromRow(_item?.Id));

		// Section break (compact)
		_sectionBreakLabel = new Label
		{
			FontSize = 12,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V4RowTablet.LookupColorThemeAware("OT_AccentFgStrong_Light", "OT_AccentFgStrong_Dark"),
		};
		_sectionBreakBorder = new Border
		{
			Padding = new Thickness(12, 6),
			Margin = new Thickness(0, 4, 0, 0),
			Background = accentSoftBrush,
			Stroke = accentBrush,
			StrokeThickness = 1,
			StrokeShape = new Rectangle(),
			Content = _sectionBreakLabel,
			IsVisible = false,
		};

		// Station cluster
		_stationNameLabel = new Label
		{
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V4RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};

		_passChipBorder = new Border
		{
			Background = ruleBrush,
			Padding = new Thickness(5, 1),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 3 },
			VerticalOptions = LayoutOptions.Center,
			Content = new Label
			{
				Text = "通過",
				FontSize = 10,
				TextColor = V4RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
			},
			IsVisible = false,
		};

		_markerBadgeLabel = new Label
		{
			FontSize = 10,
			FontAttributes = FontAttributes.Bold,
			TextColor = V4RowTablet.LookupColor("OT_MarkerFlagFg"),
		};
		_markerBadgeBorder = new Border
		{
			Padding = new Thickness(5, 1),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 4 },
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
				?? new SolidColorBrush(V4RowTablet.LookupColor("OT_AccentFgStrong_Light")),
			VerticalOptions = LayoutOptions.Center,
			IsVisible = false,
		};

		_noteToggleBorder = new Border
		{
			WidthRequest = 22,
			HeightRequest = 22,
			StrokeThickness = 0.5,
			Stroke = ruleBrush,
			StrokeShape = new RoundRectangle { CornerRadius = 11 },
			Background = bgSoftBrush,
			VerticalOptions = LayoutOptions.Center,
			Content = new Label
			{
				Text = "≡",
				FontSize = 13,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
				TextColor = V4RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
			},
			IsVisible = false,
		};
		var noteToggleTap = new TapGestureRecognizer();
		noteToggleTap.Tapped += (_, _) => InvokeOnPage(p => p.ToggleNoteForRow(_item?.Id));
		_noteToggleBorder.GestureRecognizers.Add(noteToggleTap);

		var stationCluster = new HorizontalStackLayout
		{
			Spacing = 5,
			VerticalOptions = LayoutOptions.Center,
		};
		stationCluster.Children.Add(_stationNameLabel);
		stationCluster.Children.Add(_passChipBorder);
		stationCluster.Children.Add(_markerBadgeBorder);
		stationCluster.Children.Add(_memoDot);
		stationCluster.Children.Add(_noteToggleBorder);

		// Arrive / Depart
		_arriveLabel = new Label
		{
			FontSize = 13,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V4RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_departLabel = new Label
		{
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V4RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};

		// Track chip
		_trackLabel = new Label
		{
			FontSize = 11,
			FontAttributes = FontAttributes.Bold,
			TextColor = V4RowTablet.LookupColorThemeAware("OT_PlatFg_Light", "OT_PlatFg_Dark"),
		};
		_trackBorder = new Border
		{
			Background = platBgBrush,
			Padding = new Thickness(5, 1),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 4 },
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			Content = _trackLabel,
			IsVisible = false,
		};

		// Inner grid (4 cols: *,64,64,40)
		_rowGrid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(new GridLength(64)),
				new ColumnDefinition(new GridLength(64)),
				new ColumnDefinition(new GridLength(40)),
			},
			ColumnSpacing = 6,
			Padding = new Thickness(12, 6),
		};
		Grid.SetColumn(stationCluster, 0); _rowGrid.Children.Add(stationCluster);
		Grid.SetColumn(_arriveLabel, 1);   _rowGrid.Children.Add(_arriveLabel);
		Grid.SetColumn(_departLabel, 2);   _rowGrid.Children.Add(_departLabel);
		Grid.SetColumn(_trackBorder, 3);   _rowGrid.Children.Add(_trackBorder);

		// Inline NoteFold
		_noteBodyLabel = new HtmlAutoDetectLabel
		{
			TextColor = V4RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
			FontSize = 11,
		};
		_noteBodyBorder = new Border
		{
			Margin = new Thickness(12, 0, 12, 6),
			BackgroundColor = V4RowTablet.LookupColorThemeAware("OT_BgSoft_Light", "OT_BgSoft_Dark"),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			Padding = new Thickness(8, 5),
			Content = _noteBodyLabel,
			IsVisible = false,
		};

		// Bottom rule (1pt) — see V4RowTablet ctor for rationale.
		var bottomRule = new BoxView
		{
			HeightRequest = 1,
			Color = V4RowTablet.LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark"),
		};

		var rowInner = new VerticalStackLayout { Spacing = 0 };
		rowInner.Children.Add(_rowGrid);
		rowInner.Children.Add(_noteBodyBorder);
		rowInner.Children.Add(bottomRule);

		_normalRowBorder = new Border
		{
			Background = bgBrush,
			StrokeThickness = 0,
			StrokeShape = new Rectangle(),
			Padding = new Thickness(0),
			Content = rowInner,
			IsVisible = false,
		};

		var rowStack = new VerticalStackLayout { Spacing = 0 };
		rowStack.Children.Add(_sectionBreakBorder);
		rowStack.Children.Add(_normalRowBorder);

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

		if (BindingContext is V4RowItem next)
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
			case nameof(V4RowItem.IsHiddenInList):
				ApplyVisibility();
				break;
			case nameof(V4RowItem.IsPassed):
				_normalRowBorder.Opacity = _item!.IsPassed ? 0.4 : 1.0;
				break;
			case nameof(V4RowItem.IsNoteOpen):
				_noteBodyBorder.IsVisible = _item!.IsNoteOpen;
				break;
			case nameof(V4RowItem.HasMemo):
				_memoDot.IsVisible = _item!.HasMemo;
				break;
			case nameof(V4RowItem.Marker):
			case nameof(V4RowItem.HasMarker):
			case nameof(V4RowItem.MarkerText):
			case nameof(V4RowItem.IsMarkerFlag):
			case nameof(V4RowItem.IsMarkerCaution):
			case nameof(V4RowItem.IsMarkerStar):
				ApplyMarker();
				break;
			case nameof(V4RowItem.CompactStationFontSize):
				_stationNameLabel.FontSize = _item!.CompactStationFontSize;
				break;
			case nameof(V4RowItem.CompactTimeFontSize):
				_arriveLabel.FontSize = _item!.CompactTimeFontSize;
				_departLabel.FontSize = _item.CompactTimeFontSize;
				break;
			case nameof(V4RowItem.CompactRowPadding):
				_rowGrid.Padding = _item!.CompactRowPadding;
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
		_sectionBreakLabel.Text       = _item.SectionBreakLabel;
		ApplyVisibility();

		_stationNameLabel.Text     = _item.StationName;
		_stationNameLabel.FontSize = _item.CompactStationFontSize;
		_stationNameLabel.TextColor = _item.IsPass
			? V4RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark")
			: V4RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark");
		_passChipBorder.IsVisible = _item.IsPass;

		_arriveLabel.Text     = _item.ArriveText;
		_arriveLabel.FontSize = _item.CompactTimeFontSize;
		_departLabel.Text     = _item.DepartText;
		_departLabel.FontSize = _item.CompactTimeFontSize;
		_rowGrid.Padding      = _item.CompactRowPadding;

		_trackLabel.Text       = _item.TrackName;
		_trackBorder.IsVisible = _item.HasTrackName;

		_noteToggleBorder.IsVisible = _item.HasNote;
		_noteBodyLabel.Text         = _item.NoteText;
		_noteBodyBorder.IsVisible   = _item.IsNoteOpen;

		_normalRowBorder.Opacity = _item.IsPassed ? 0.4 : 1.0;

		ApplyMarker();
		_memoDot.IsVisible = _item.HasMemo;
	}

	void ApplyVisibility()
	{
		if (_item is null)
			return;
		_normalRowBorder.IsVisible = _item.IsVisibleNormalRow;
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
				?? new SolidColorBrush(V4RowTablet.LookupColor("OT_MarkerFlagBg"));
			_markerBadgeLabel.TextColor = V4RowTablet.LookupColor("OT_MarkerFlagFg");
		}
		else if (_item.IsMarkerCaution)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerCautionBgBrush"]
				?? new SolidColorBrush(V4RowTablet.LookupColor("OT_MarkerCautionBg"));
			_markerBadgeLabel.TextColor = V4RowTablet.LookupColor("OT_MarkerCautionFg");
		}
		else if (_item.IsMarkerStar)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerStarBgBrush"]
				?? new SolidColorBrush(V4RowTablet.LookupColor("OT_MarkerStarBg"));
			_markerBadgeLabel.TextColor = V4RowTablet.LookupColor("OT_MarkerStarFg");
		}
		else
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_Bg"]
				?? new SolidColorBrush(Colors.White);
			_markerBadgeLabel.TextColor = V4RowTablet.LookupColor("OT_MarkerFlagFg");
		}
	}

	void InvokeOnPage(Action<OriginalTimetableV4Page> action)
	{
		Element? cur = this;
		while (cur is not null)
		{
			if (cur is OriginalTimetableV4Page page)
			{
				action(page);
				return;
			}
			cur = cur.Parent;
		}
	}
}

#endregion
