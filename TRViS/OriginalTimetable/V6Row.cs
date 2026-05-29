// V6Row.cs — Programmatic row Views for OriginalTimetableV6Page (Bold Editorial).
//
// Mirrors the V1Row.cs / V2Row.cs / V4Row.cs workaround for the Apple Swift
// ObservationTracking `_AccessList` / `_NativeDictionary.copy` use-after-free
// (swiftlang#84228): the V6 upcoming list previously hosted a `SwipeView` +
// `HtmlAutoDetectLabel` (inline NoteFold) inside `<CollectionView>` +
// `<DataTemplate>`, which routes through MAUI's iOS UICollectionView handler
// → SwiftUI ViewGraph and trips the crash during cell recycling. Replacing
// the CollectionView with BindableLayout-on-VSL keeps the visual identical
// but emits plain Element siblings — no ViewGraph path.
//
// Two row classes match V1 / V2 / V4's tablet/compact split:
//   • V6RowTablet   — 5-column row (counter / station+badges / arrive / depart / track)
//   • V6RowCompact  — same shape at smaller scale
//
// V6 differs from V4 in several ways worth flagging:
//   1) No "current emphasis" or IsPassed/IsHiddenInList — V6 splits past /
//      current / upcoming at the page level (past=chips, current=hero block,
//      upcoming=this list). Every row in Items is upcoming, so no per-row
//      current/passed styling.
//   2) Counter column (col 0) holds a zero-padded "01"-style index distinct
//      from the station id; V4 has no counter.
//   3) ArriveText is muted and non-bold; DepartText is Fg-color and bold —
//      V6's "Bold Editorial" identity emphasises departure-time legibility.
//   4) Section break is a heavyweight double-rule (top+bottom 2pt Fg stroke)
//      with bold Fg label — V4 uses a 1pt accent stroke + accent-soft fill.
//   5) Track chip is a square 40×40 (tablet) / 32×32 (compact) Border with
//      2pt Fg stroke and Menlo bold text — V4 uses a small rounded-rect chip.
//   6) Inline NoteFold uses HtmlAutoDetectLabel (same crash trigger as V4).
//
// SwipeItems use `.Invoked` + a Parent-walk to the page (same pattern as
// V1/V2/V4), because Command bindings inside a programmatic SwipeItem can't
// resolve `Source={x:Reference Self}` against the XAML root.

using System.ComponentModel;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

#region Tablet layout (≥ 600pt)

/// <summary>
/// Tablet-layout upcoming-list row View for the V6 page. Mirrors the 5-column
/// row that previously lived inside <c>TabletUpcomingList</c> (CollectionView).
/// Subscribes to V6RowItem INPC on <c>OnBindingContextChanged</c>; releases on
/// context swap.
/// </summary>
public sealed class V6RowTablet : ContentView
{
	V6RowItem? _item;

	// Sub-views we mutate when V6RowItem properties change.
	readonly SwipeItem _swipeMarker;
	readonly SwipeItem _swipeMemo;
	readonly SwipeItem _swipeClear;

	readonly Border _sectionBreakBorder;
	readonly Label _sectionBreakLabel;

	readonly Border _normalRowBorder;
	readonly Grid _rowGrid;
	readonly Label _counterLabel;
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

	public V6RowTablet()
	{
		var res = Application.Current?.Resources;
		Brush bgBrush     = (Brush?)res?["OT_Bg"]     ?? new SolidColorBrush(Colors.White);
		Brush bgSoftBrush = (Brush?)res?["OT_BgSoft"] ?? new SolidColorBrush(Colors.LightGray);
		Brush ruleBrush   = (Brush?)res?["OT_Rule"]   ?? new SolidColorBrush(Colors.Gray);
		Brush fgBrush     = (Brush?)res?["OT_Fg"]     ?? new SolidColorBrush(Colors.Black);

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

		// ── Section-break row (V6 editorial: double 2pt Fg rule, bold Fg label) ─
		_sectionBreakLabel = new Label
		{
			FontSize = 15,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		// Border.StrokeThickness is double, so a uniform 2pt Fg stroke is the
		// closest expressible analog to XAML's sided "0,2,0,2" (top+bottom only).
		_sectionBreakBorder = new Border
		{
			Padding = new Thickness(16, 10),
			Margin = new Thickness(0, 6, 0, 0),
			Background = bgBrush,
			Stroke = fgBrush,
			StrokeThickness = 2,
			StrokeShape = new Rectangle(),
			Content = _sectionBreakLabel,
			IsVisible = false,
		};

		// ── Counter (col 0) ──────────────────────────────────────────────────
		_counterLabel = new Label
		{
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};

		// ── Station cluster (col 1) ──────────────────────────────────────────
		_stationNameLabel = new HtmlAutoDetectLabel
		{
			FontSize = 22,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
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
			Spacing = 8,
			VerticalOptions = LayoutOptions.Center,
		};
		stationCluster.Children.Add(_stationNameLabel);
		stationCluster.Children.Add(_markerBadgeBorder);
		stationCluster.Children.Add(_memoDot);
		stationCluster.Children.Add(_noteToggleBorder);

		// ── Arrive (col 2, muted, non-bold) / Depart (col 3, fg, bold) ───────
		_arriveLabel = new Label
		{
			FontSize = 18,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_departLabel = new Label
		{
			FontSize = 18,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};

		// ── Track chip (col 4 — square 40×40, 2pt Fg stroke, V6-distinctive) ─
		_trackLabel = new HtmlAutoDetectLabel
		{
			Text = string.Empty,
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_trackBorder = new Border
		{
			StrokeThickness = 2,
			Stroke = fgBrush,
			StrokeShape = new Rectangle(),
			BackgroundColor = Colors.Transparent,
			WidthRequest = 40,
			HeightRequest = 40,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Content = _trackLabel,
			IsVisible = false,
		};

		// ── Inner grid (5 cols: 44,*,80,80,50) ───────────────────────────────
		_rowGrid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(new GridLength(44)),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(new GridLength(80)),
				new ColumnDefinition(new GridLength(80)),
				new ColumnDefinition(new GridLength(50)),
			},
			ColumnSpacing = 8,
			Padding = new Thickness(16, 10),
		};
		Grid.SetColumn(_counterLabel, 0);   _rowGrid.Children.Add(_counterLabel);
		Grid.SetColumn(stationCluster, 1);  _rowGrid.Children.Add(stationCluster);
		Grid.SetColumn(_arriveLabel, 2);    _rowGrid.Children.Add(_arriveLabel);
		Grid.SetColumn(_departLabel, 3);    _rowGrid.Children.Add(_departLabel);
		Grid.SetColumn(_trackBorder, 4);    _rowGrid.Children.Add(_trackBorder);

		// ── Inline NoteFold (HtmlAutoDetectLabel — the very thing this refactor
		//    moves out of CollectionView; safe under BindableLayout). ──────────
		_noteBodyLabel = new HtmlAutoDetectLabel
		{
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
			FontSize = 12,
		};
		_noteBodyBorder = new Border
		{
			Margin = new Thickness(16, 0, 16, 10),
			BackgroundColor = LookupColorThemeAware("OT_BgSoft_Light", "OT_BgSoft_Dark"),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			Padding = new Thickness(10, 6),
			Content = _noteBodyLabel,
			IsVisible = false,
		};

		// Bottom rule (1pt) — replaces XAML's sided "0,0,0,1" rule. Border.
		// StrokeThickness is double, so render as a BoxView sibling at bottom.
		var bottomRule = new BoxView
		{
			HeightRequest = 1,
			Color = LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark"),
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

		if (BindingContext is V6RowItem next)
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
			case nameof(V6RowItem.IsNoteOpen):
				_noteBodyBorder.IsVisible = _item!.IsNoteOpen;
				break;
			case nameof(V6RowItem.HasMemo):
				_memoDot.IsVisible = _item!.HasMemo;
				break;
			case nameof(V6RowItem.Marker):
			case nameof(V6RowItem.HasMarker):
			case nameof(V6RowItem.MarkerText):
			case nameof(V6RowItem.IsMarkerFlag):
			case nameof(V6RowItem.IsMarkerCaution):
			case nameof(V6RowItem.IsMarkerStar):
				ApplyMarker();
				break;
			case nameof(V6RowItem.TabletStationFontSize):
				_stationNameLabel.FontSize = _item!.TabletStationFontSize;
				break;
			case nameof(V6RowItem.TabletTimeFontSize):
				_arriveLabel.FontSize = _item!.TabletTimeFontSize;
				_departLabel.FontSize = _item.TabletTimeFontSize;
				break;
			case nameof(V6RowItem.TabletRowPadding):
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
		_normalRowBorder.IsVisible    = _item.IsNormalRow;

		_counterLabel.Text         = _item.CounterText;

		_stationNameLabel.Text     = _item.StationName;
		_stationNameLabel.FontSize = _item.TabletStationFontSize;
		// V6 IsPass styling: muted color + non-bold (matches XAML DataTrigger).
		if (_item.IsPass)
		{
			_stationNameLabel.TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark");
			_stationNameLabel.FontAttributes = FontAttributes.None;
		}
		else
		{
			_stationNameLabel.TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark");
			_stationNameLabel.FontAttributes = FontAttributes.Bold;
		}

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

		ApplyMarker();
		_memoDot.IsVisible = _item.HasMemo;
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

	void InvokeOnPage(Action<OriginalTimetableV6Page> action)
	{
		Element? cur = this;
		while (cur is not null)
		{
			if (cur is OriginalTimetableV6Page page)
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
/// Compact-layout upcoming-list row View. Same shape as <see cref="V6RowTablet"/>
/// at smaller scale (compact column widths 36,*,64,64,40; compact font scales;
/// tighter padding). Subscribes to V6RowItem.Compact* properties for the
/// in-place density updates.
/// </summary>
public sealed class V6RowCompact : ContentView
{
	V6RowItem? _item;

	readonly SwipeItem _swipeMarker;
	readonly SwipeItem _swipeMemo;
	readonly SwipeItem _swipeClear;

	readonly Border _sectionBreakBorder;
	readonly Label _sectionBreakLabel;

	readonly Border _normalRowBorder;
	readonly Grid _rowGrid;
	readonly Label _counterLabel;
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

	public V6RowCompact()
	{
		var res = Application.Current?.Resources;
		Brush bgBrush     = (Brush?)res?["OT_Bg"]     ?? new SolidColorBrush(Colors.White);
		Brush bgSoftBrush = (Brush?)res?["OT_BgSoft"] ?? new SolidColorBrush(Colors.LightGray);
		Brush ruleBrush   = (Brush?)res?["OT_Rule"]   ?? new SolidColorBrush(Colors.Gray);
		Brush fgBrush     = (Brush?)res?["OT_Fg"]     ?? new SolidColorBrush(Colors.Black);

		_swipeMarker = new SwipeItem
		{
			Text = "マーカー",
			BackgroundColor = V6RowTablet.LookupColor("OT_Accent_Light"),
		};
		_swipeMarker.Invoked += (_, _) => InvokeOnPage(p => p.OpenMarkerPopoverFromSwipe(_item?.Id));

		_swipeMemo = new SwipeItem
		{
			Text = "メモ",
			BackgroundColor = V6RowTablet.LookupColor("OT_MarkerCautionBg"),
		};
		_swipeMemo.Invoked += (_, _) => InvokeOnPage(p => p.OpenMemoFromRow(_item?.Id));

		_swipeClear = new SwipeItem
		{
			Text = "クリア",
			BackgroundColor = V6RowTablet.LookupColor("OT_AccentSoft_Light"),
		};
		_swipeClear.Invoked += (_, _) => InvokeOnPage(p => p.ClearMarkerFromRow(_item?.Id));

		// Section break (compact — same V6 double-rule style at smaller scale)
		_sectionBreakLabel = new Label
		{
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V6RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_sectionBreakBorder = new Border
		{
			Padding = new Thickness(12, 8),
			Margin = new Thickness(0, 4, 0, 0),
			Background = bgBrush,
			Stroke = fgBrush,
			StrokeThickness = 2,
			StrokeShape = new Rectangle(),
			Content = _sectionBreakLabel,
			IsVisible = false,
		};

		// Counter (col 0, compact)
		_counterLabel = new Label
		{
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			VerticalOptions = LayoutOptions.Center,
			TextColor = V6RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};

		// Station cluster
		_stationNameLabel = new HtmlAutoDetectLabel
		{
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V6RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};

		_markerBadgeLabel = new Label
		{
			FontSize = 10,
			FontAttributes = FontAttributes.Bold,
			TextColor = V6RowTablet.LookupColor("OT_MarkerFlagFg"),
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
				?? new SolidColorBrush(V6RowTablet.LookupColor("OT_AccentFgStrong_Light")),
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
				FontSize = 12,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
				TextColor = V6RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
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
		stationCluster.Children.Add(_markerBadgeBorder);
		stationCluster.Children.Add(_memoDot);
		stationCluster.Children.Add(_noteToggleBorder);

		// Arrive (muted) / Depart (fg bold) — V6 editorial split.
		_arriveLabel = new Label
		{
			FontSize = 14,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V6RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_departLabel = new Label
		{
			FontSize = 14,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V6RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};

		// Track chip (square 32×32, 2pt Fg stroke)
		_trackLabel = new HtmlAutoDetectLabel
		{
			Text = string.Empty,
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V6RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_trackBorder = new Border
		{
			StrokeThickness = 2,
			Stroke = fgBrush,
			StrokeShape = new Rectangle(),
			BackgroundColor = Colors.Transparent,
			WidthRequest = 32,
			HeightRequest = 32,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Content = _trackLabel,
			IsVisible = false,
		};

		// Inner grid (5 cols: 36,*,64,64,40)
		_rowGrid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(new GridLength(36)),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(new GridLength(64)),
				new ColumnDefinition(new GridLength(64)),
				new ColumnDefinition(new GridLength(40)),
			},
			ColumnSpacing = 6,
			Padding = new Thickness(12, 8),
		};
		Grid.SetColumn(_counterLabel, 0);   _rowGrid.Children.Add(_counterLabel);
		Grid.SetColumn(stationCluster, 1);  _rowGrid.Children.Add(stationCluster);
		Grid.SetColumn(_arriveLabel, 2);    _rowGrid.Children.Add(_arriveLabel);
		Grid.SetColumn(_departLabel, 3);    _rowGrid.Children.Add(_departLabel);
		Grid.SetColumn(_trackBorder, 4);    _rowGrid.Children.Add(_trackBorder);

		// Inline NoteFold
		_noteBodyLabel = new HtmlAutoDetectLabel
		{
			TextColor = V6RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
			FontSize = 11,
		};
		_noteBodyBorder = new Border
		{
			Margin = new Thickness(12, 0, 12, 8),
			BackgroundColor = V6RowTablet.LookupColorThemeAware("OT_BgSoft_Light", "OT_BgSoft_Dark"),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			Padding = new Thickness(8, 5),
			Content = _noteBodyLabel,
			IsVisible = false,
		};

		// Bottom rule (1pt) — see V6RowTablet ctor for rationale.
		var bottomRule = new BoxView
		{
			HeightRequest = 1,
			Color = V6RowTablet.LookupColorThemeAware("OT_Rule_Light", "OT_Rule_Dark"),
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

		if (BindingContext is V6RowItem next)
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
			case nameof(V6RowItem.IsNoteOpen):
				_noteBodyBorder.IsVisible = _item!.IsNoteOpen;
				break;
			case nameof(V6RowItem.HasMemo):
				_memoDot.IsVisible = _item!.HasMemo;
				break;
			case nameof(V6RowItem.Marker):
			case nameof(V6RowItem.HasMarker):
			case nameof(V6RowItem.MarkerText):
			case nameof(V6RowItem.IsMarkerFlag):
			case nameof(V6RowItem.IsMarkerCaution):
			case nameof(V6RowItem.IsMarkerStar):
				ApplyMarker();
				break;
			case nameof(V6RowItem.CompactStationFontSize):
				_stationNameLabel.FontSize = _item!.CompactStationFontSize;
				break;
			case nameof(V6RowItem.CompactTimeFontSize):
				_arriveLabel.FontSize = _item!.CompactTimeFontSize;
				_departLabel.FontSize = _item.CompactTimeFontSize;
				break;
			case nameof(V6RowItem.CompactRowPadding):
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
		_normalRowBorder.IsVisible    = _item.IsNormalRow;

		_counterLabel.Text         = _item.CounterText;

		_stationNameLabel.Text     = _item.StationName;
		_stationNameLabel.FontSize = _item.CompactStationFontSize;
		if (_item.IsPass)
		{
			_stationNameLabel.TextColor = V6RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark");
			_stationNameLabel.FontAttributes = FontAttributes.None;
		}
		else
		{
			_stationNameLabel.TextColor = V6RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark");
			_stationNameLabel.FontAttributes = FontAttributes.Bold;
		}

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

		ApplyMarker();
		_memoDot.IsVisible = _item.HasMemo;
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
				?? new SolidColorBrush(V6RowTablet.LookupColor("OT_MarkerFlagBg"));
			_markerBadgeLabel.TextColor = V6RowTablet.LookupColor("OT_MarkerFlagFg");
		}
		else if (_item.IsMarkerCaution)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerCautionBgBrush"]
				?? new SolidColorBrush(V6RowTablet.LookupColor("OT_MarkerCautionBg"));
			_markerBadgeLabel.TextColor = V6RowTablet.LookupColor("OT_MarkerCautionFg");
		}
		else if (_item.IsMarkerStar)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerStarBgBrush"]
				?? new SolidColorBrush(V6RowTablet.LookupColor("OT_MarkerStarBg"));
			_markerBadgeLabel.TextColor = V6RowTablet.LookupColor("OT_MarkerStarFg");
		}
		else
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_Bg"]
				?? new SolidColorBrush(Colors.White);
			_markerBadgeLabel.TextColor = V6RowTablet.LookupColor("OT_MarkerFlagFg");
		}
	}

	void InvokeOnPage(Action<OriginalTimetableV6Page> action)
	{
		Element? cur = this;
		while (cur is not null)
		{
			if (cur is OriginalTimetableV6Page page)
			{
				action(page);
				return;
			}
			cur = cur.Parent;
		}
	}
}

#endregion
