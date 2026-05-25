// V2Row.cs — Programmatic row Views for OriginalTimetableV2Page (Card Stack).
//
// Mirrors the V1Row.cs workaround for the Apple Swift ObservationTracking
// `_AccessList` / `_NativeDictionary.copy` use-after-free (swiftlang#84228):
// the V2 XAML used to wrap a `SwipeView` + `HtmlAutoDetectLabel` inside
// `<CollectionView>` + `<DataTemplate>`, which routes through MAUI's iOS
// UICollectionView handler → SwiftUI ViewGraph and trips the crash during
// cell recycling. Replacing CollectionView with BindableLayout-on-VSL keeps
// the visual identical but emits plain Element siblings — no ViewGraph path.
//
// Two row classes match V1's tablet/compact split:
//   • V2RowTablet   — 4-column card (run / station+badges / arrive+depart / platform)
//   • V2RowCompact  — same 4-column shape at smaller scale
//
// V2 differs from V1 on three points worth flagging:
//   1) Many more observable per-card metrics. ApplyCurrentAndDensityScaledMetrics
//      writes TabletPlatformSize/FontSize, TabletTimeFontSize, TabletStationFontSize
//      (and their Compact equivalents) plus the *CardMargin every time IsCurrent
//      or vm.Density changes. INPC subscriptions wire each prop individually.
//   2) IsCurrent does more visual work — outer card Background/Stroke/StrokeThickness
//      flip, station-name TextColor swaps to AccentFgStrong (overrides IsPass-muted),
//      platform tile Background → Accent, platform Label → AccentFg.
//   3) NoteFold lives *inside* the outer card Border so concentric radii hold;
//      V1 stacks the note as a sibling below the row Border.
//
// SwipeItems use `.Invoked` + a Parent-walk to the page (same pattern as V1),
// because Command bindings inside a programmatic SwipeItem can't resolve
// `Source={x:Reference Self}` against the XAML root.

using System.ComponentModel;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.ViewModels;

namespace TRViS.OriginalTimetable;

#region Tablet layout (≥ 600pt)

/// <summary>
/// Tablet-layout card View for the V2 page. Mirrors the 4-column card that
/// previously lived inside <c>TabletRowsList</c> (CollectionView). Subscribes
/// to V2RowItem INPC on <c>OnBindingContextChanged</c>; releases on context
/// swap.
/// </summary>
public sealed class V2RowTablet : ContentView
{
	V2RowItem? _item;

	// Sub-views we mutate when V2RowItem properties change.
	readonly SwipeItem _swipeMarker;
	readonly SwipeItem _swipeMemo;
	readonly SwipeItem _swipeClear;

	readonly Border _sectionBreakBorder;
	readonly Label _sectionBreakLabel;

	readonly Border _normalCardBorder;
	readonly Label _runLabel;
	readonly Label _stationNameLabel;
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

	public V2RowTablet()
	{
		var res = Application.Current?.Resources;
		Brush bgBrush     = (Brush?)res?["OT_Bg"]     ?? new SolidColorBrush(Colors.White);
		Brush bgSoftBrush = (Brush?)res?["OT_BgSoft"] ?? new SolidColorBrush(Colors.LightGray);
		Brush ruleBrush   = (Brush?)res?["OT_Rule"]   ?? new SolidColorBrush(Colors.Gray);
		Brush platBgBrush = (Brush?)res?["OT_PlatBg"] ?? new SolidColorBrush(Colors.LightGray);

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

		// ── Section-break card ────────────────────────────────────────────────
		_sectionBreakLabel = new Label
		{
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_AccentFgStrong_Light", "OT_AccentFgStrong_Dark"),
		};
		_sectionBreakBorder = new Border
		{
			Margin = new Thickness(8, 8),
			Padding = new Thickness(12, 6),
			Background = bgSoftBrush,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 10 },
			Content = _sectionBreakLabel,
			IsVisible = false,
		};

		// ── Run column (2 stacked labels) ─────────────────────────────────────
		_runLabel = new Label
		{
			FontSize = 22,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		var runCaption = new Label
		{
			Text = "運転",
			FontSize = 10,
			HorizontalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		var runColumn = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Spacing = 2,
		};
		runColumn.Children.Add(_runLabel);
		runColumn.Children.Add(runCaption);

		// ── Station cluster ───────────────────────────────────────────────────
		_stationNameLabel = new Label
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
			Spacing = 6,
			VerticalOptions = LayoutOptions.Center,
		};
		stationCluster.Children.Add(_stationNameLabel);
		stationCluster.Children.Add(_markerBadgeBorder);
		stationCluster.Children.Add(_memoDot);
		stationCluster.Children.Add(_noteToggleBorder);

		// ── Arrive/Depart column ──────────────────────────────────────────────
		var arrivePrefix = new Label
		{
			Text = "着",
			FontSize = 14,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_arriveLabel = new Label
		{
			FontSize = 22,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		var arriveRow = new HorizontalStackLayout
		{
			Spacing = 6,
			HorizontalOptions = LayoutOptions.End,
		};
		arriveRow.Children.Add(arrivePrefix);
		arriveRow.Children.Add(_arriveLabel);

		var departPrefix = new Label
		{
			Text = "発",
			FontSize = 14,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_departLabel = new Label
		{
			FontSize = 22,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		var departRow = new HorizontalStackLayout
		{
			Spacing = 6,
			HorizontalOptions = LayoutOptions.End,
		};
		departRow.Children.Add(departPrefix);
		departRow.Children.Add(_departLabel);

		var timeColumn = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			Spacing = 2,
		};
		timeColumn.Children.Add(arriveRow);
		timeColumn.Children.Add(departRow);

		// ── Platform tile (square, inner-concentric radius 8) ─────────────────
		_trackLabel = new Label
		{
			FontSize = 22,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			TextColor = LookupColorThemeAware("OT_PlatFg_Light", "OT_PlatFg_Dark"),
		};
		_trackBorder = new Border
		{
			Background = platBgBrush,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			WidthRequest = 56,
			HeightRequest = 56,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Content = _trackLabel,
			IsVisible = false,
		};

		// ── Inner card grid (4 cols) ──────────────────────────────────────────
		var cardGrid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(new GridLength(70)),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(new GridLength(76)),
			},
			ColumnSpacing = 10,
			RowSpacing = 0,
		};
		Grid.SetColumn(runColumn, 0);      cardGrid.Children.Add(runColumn);
		Grid.SetColumn(stationCluster, 1); cardGrid.Children.Add(stationCluster);
		Grid.SetColumn(timeColumn, 2);     cardGrid.Children.Add(timeColumn);
		Grid.SetColumn(_trackBorder, 3);   cardGrid.Children.Add(_trackBorder);

		// ── Inline NoteFold (inside the card so concentric radii hold). The
		// HtmlAutoDetectLabel is the previously-crashing control; safe here
		// because it lives inside a BindableLayout-rendered tree (plain VSL
		// parent), not a UICollectionView/ViewGraph.
		_noteBodyLabel = new HtmlAutoDetectLabel
		{
			TextColor = LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
			FontSize = 13,
		};
		_noteBodyBorder = new Border
		{
			BackgroundColor = LookupColorThemeAware("OT_BgSoft_Light", "OT_BgSoft_Dark"),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Padding = new Thickness(10, 6),
			Content = _noteBodyLabel,
			IsVisible = false,
		};

		var cardInner = new VerticalStackLayout { Spacing = 8 };
		cardInner.Children.Add(cardGrid);
		cardInner.Children.Add(_noteBodyBorder);

		_normalCardBorder = new Border
		{
			Margin = new Thickness(8, 4),
			Padding = new Thickness(12),
			Background = bgBrush,
			Stroke = ruleBrush,
			StrokeThickness = 1,
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Content = cardInner,
			IsVisible = false,
		};

		// ── Compose ───────────────────────────────────────────────────────────
		var rowStack = new VerticalStackLayout { Spacing = 0 };
		rowStack.Children.Add(_sectionBreakBorder);
		rowStack.Children.Add(_normalCardBorder);

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

		if (BindingContext is V2RowItem next)
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
			case nameof(V2RowItem.IsCurrent):
				ApplyCurrent();
				break;
			case nameof(V2RowItem.IsNoteOpen):
				ApplyNoteOpen();
				break;
			case nameof(V2RowItem.HasMemo):
				_memoDot.IsVisible = _item!.HasMemo;
				break;
			case nameof(V2RowItem.Marker):
			case nameof(V2RowItem.HasMarker):
			case nameof(V2RowItem.MarkerText):
			case nameof(V2RowItem.IsMarkerFlag):
			case nameof(V2RowItem.IsMarkerCaution):
			case nameof(V2RowItem.IsMarkerStar):
				ApplyMarker();
				break;
			case nameof(V2RowItem.TabletPlatformSize):
				_trackBorder.WidthRequest = _item!.TabletPlatformSize;
				_trackBorder.HeightRequest = _item.TabletPlatformSize;
				break;
			case nameof(V2RowItem.TabletPlatformFontSize):
				_trackLabel.FontSize = _item!.TabletPlatformFontSize;
				break;
			case nameof(V2RowItem.TabletTimeFontSize):
				_arriveLabel.FontSize = _item!.TabletTimeFontSize;
				_departLabel.FontSize = _item.TabletTimeFontSize;
				break;
			case nameof(V2RowItem.TabletStationFontSize):
				_stationNameLabel.FontSize = _item!.TabletStationFontSize;
				break;
			case nameof(V2RowItem.TabletCardMargin):
				_normalCardBorder.Margin = _item!.TabletCardMargin;
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
		_normalCardBorder.IsVisible   = !_item.IsSectionBreakRow;

		_sectionBreakLabel.Text = _item.SectionBreakLabel;

		_runLabel.Text         = _item.RunText;
		_stationNameLabel.Text = _item.StationName;
		_stationNameLabel.FontSize = _item.TabletStationFontSize;
		_arriveLabel.Text  = _item.ArriveText;
		_arriveLabel.FontSize = _item.TabletTimeFontSize;
		_departLabel.Text  = _item.DepartText;
		_departLabel.FontSize = _item.TabletTimeFontSize;
		_trackLabel.Text   = _item.TrackName;
		_trackLabel.FontSize = _item.TabletPlatformFontSize;
		_trackBorder.IsVisible = _item.HasTrackName;
		_trackBorder.WidthRequest = _item.TabletPlatformSize;
		_trackBorder.HeightRequest = _item.TabletPlatformSize;
		_normalCardBorder.Margin = _item.TabletCardMargin;

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

		var res = Application.Current?.Resources;
		if (_item.IsCurrent)
		{
			_normalCardBorder.Background = (Brush?)res?["OT_AccentSoft"]
				?? new SolidColorBrush(LookupColor("OT_AccentSoft_Light"));
			_normalCardBorder.Stroke = (Brush?)res?["OT_Accent"]
				?? new SolidColorBrush(LookupColor("OT_Accent_Light"));
			_normalCardBorder.StrokeThickness = 1.5;
			_stationNameLabel.TextColor = LookupColorThemeAware("OT_AccentFgStrong_Light", "OT_AccentFgStrong_Dark");
			_trackBorder.Background = (Brush?)res?["OT_Accent"]
				?? new SolidColorBrush(LookupColor("OT_Accent_Light"));
			_trackLabel.TextColor = LookupColorThemeAware("OT_AccentFg_Light", "OT_AccentFg_Dark");
		}
		else
		{
			_normalCardBorder.Background = (Brush?)res?["OT_Bg"]
				?? new SolidColorBrush(Colors.White);
			_normalCardBorder.Stroke = (Brush?)res?["OT_Rule"]
				?? new SolidColorBrush(Colors.Gray);
			_normalCardBorder.StrokeThickness = 1;
			// IsPass-muted only kicks in when IsCurrent is false (IsCurrent wins).
			_stationNameLabel.TextColor = _item.IsPass
				? LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark")
				: LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark");
			_trackBorder.Background = (Brush?)res?["OT_PlatBg"]
				?? new SolidColorBrush(Colors.LightGray);
			_trackLabel.TextColor = LookupColorThemeAware("OT_PlatFg_Light", "OT_PlatFg_Dark");
		}
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

	void ApplyNoteOpen()
	{
		if (_item is null)
			return;
		_noteBodyBorder.IsVisible = _item.IsNoteOpen;
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	internal static Color LookupColor(string key)
		=> (Application.Current?.Resources[key] as Color) ?? Colors.Black;

	internal static Color LookupColorThemeAware(string lightKey, string darkKey)
	{
		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		return LookupColor(isDark ? darkKey : lightKey);
	}

	void InvokeOnPage(Action<OriginalTimetableV2Page> action)
	{
		Element? cur = this;
		while (cur is not null)
		{
			if (cur is OriginalTimetableV2Page page)
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
/// Compact-layout card View. Same shape as <see cref="V2RowTablet"/> at
/// smaller scale (compact platform size 44/52, compact font scales, tighter
/// margins). Subscribes to V2RowItem.Compact* properties for the in-place
/// IsCurrent + density updates.
/// </summary>
public sealed class V2RowCompact : ContentView
{
	V2RowItem? _item;

	readonly SwipeItem _swipeMarker;
	readonly SwipeItem _swipeMemo;
	readonly SwipeItem _swipeClear;

	readonly Border _sectionBreakBorder;
	readonly Label _sectionBreakLabel;

	readonly Border _normalCardBorder;
	readonly Label _runLabel;
	readonly Label _stationNameLabel;
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

	public V2RowCompact()
	{
		var res = Application.Current?.Resources;
		Brush bgBrush     = (Brush?)res?["OT_Bg"]     ?? new SolidColorBrush(Colors.White);
		Brush bgSoftBrush = (Brush?)res?["OT_BgSoft"] ?? new SolidColorBrush(Colors.LightGray);
		Brush ruleBrush   = (Brush?)res?["OT_Rule"]   ?? new SolidColorBrush(Colors.Gray);
		Brush platBgBrush = (Brush?)res?["OT_PlatBg"] ?? new SolidColorBrush(Colors.LightGray);

		_swipeMarker = new SwipeItem
		{
			Text = "マーカー",
			BackgroundColor = V2RowTablet.LookupColor("OT_Accent_Light"),
		};
		_swipeMarker.Invoked += (_, _) => InvokeOnPage(p => p.OpenMarkerPopoverFromSwipe(_item?.Id));

		_swipeMemo = new SwipeItem
		{
			Text = "メモ",
			BackgroundColor = V2RowTablet.LookupColor("OT_MarkerCautionBg"),
		};
		_swipeMemo.Invoked += (_, _) => InvokeOnPage(p => p.OpenMemoFromRow(_item?.Id));

		_swipeClear = new SwipeItem
		{
			Text = "クリア",
			BackgroundColor = V2RowTablet.LookupColor("OT_AccentSoft_Light"),
		};
		_swipeClear.Invoked += (_, _) => InvokeOnPage(p => p.ClearMarkerFromRow(_item?.Id));

		// Section break (compact)
		_sectionBreakLabel = new Label
		{
			FontSize = 11,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_AccentFgStrong_Light", "OT_AccentFgStrong_Dark"),
		};
		_sectionBreakBorder = new Border
		{
			Margin = new Thickness(6, 6),
			Padding = new Thickness(10, 5),
			Background = bgSoftBrush,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Content = _sectionBreakLabel,
			IsVisible = false,
		};

		// Run column
		_runLabel = new Label
		{
			FontSize = 14,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.Center,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		var runCaption = new Label
		{
			Text = "運転",
			FontSize = 9,
			HorizontalOptions = LayoutOptions.Center,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		var runColumn = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Spacing = 1,
		};
		runColumn.Children.Add(_runLabel);
		runColumn.Children.Add(runCaption);

		// Station cluster
		_stationNameLabel = new Label
		{
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		_markerBadgeLabel = new Label
		{
			FontSize = 10,
			FontAttributes = FontAttributes.Bold,
			TextColor = V2RowTablet.LookupColor("OT_MarkerFlagFg"),
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
				?? new SolidColorBrush(V2RowTablet.LookupColor("OT_AccentFgStrong_Light")),
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
				TextColor = V2RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
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
		stationCluster.Children.Add(_markerBadgeBorder);
		stationCluster.Children.Add(_memoDot);
		stationCluster.Children.Add(_noteToggleBorder);

		// Arrive/Depart column
		var arrivePrefix = new Label
		{
			Text = "着",
			FontSize = 10,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_arriveLabel = new Label
		{
			FontSize = 14,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		var arriveRow = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };
		arriveRow.Children.Add(arrivePrefix);
		arriveRow.Children.Add(_arriveLabel);

		var departPrefix = new Label
		{
			Text = "発",
			FontSize = 10,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark"),
		};
		_departLabel = new Label
		{
			FontSize = 14,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.End,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
		};
		var departRow = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };
		departRow.Children.Add(departPrefix);
		departRow.Children.Add(_departLabel);

		var timeColumn = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			Spacing = 1,
		};
		timeColumn.Children.Add(arriveRow);
		timeColumn.Children.Add(departRow);

		// Platform tile
		_trackLabel = new Label
		{
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = "Menlo",
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			TextColor = V2RowTablet.LookupColorThemeAware("OT_PlatFg_Light", "OT_PlatFg_Dark"),
		};
		_trackBorder = new Border
		{
			Background = platBgBrush,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			WidthRequest = 44,
			HeightRequest = 44,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Content = _trackLabel,
			IsVisible = false,
		};

		var cardGrid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(new GridLength(60)),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(new GridLength(64)),
			},
			ColumnSpacing = 8,
			RowSpacing = 0,
		};
		Grid.SetColumn(runColumn, 0);      cardGrid.Children.Add(runColumn);
		Grid.SetColumn(stationCluster, 1); cardGrid.Children.Add(stationCluster);
		Grid.SetColumn(timeColumn, 2);     cardGrid.Children.Add(timeColumn);
		Grid.SetColumn(_trackBorder, 3);   cardGrid.Children.Add(_trackBorder);

		// Inline NoteFold (inside card)
		_noteBodyLabel = new HtmlAutoDetectLabel
		{
			TextColor = V2RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark"),
			FontSize = 12,
		};
		_noteBodyBorder = new Border
		{
			BackgroundColor = V2RowTablet.LookupColorThemeAware("OT_BgSoft_Light", "OT_BgSoft_Dark"),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			Padding = new Thickness(8, 5),
			Content = _noteBodyLabel,
			IsVisible = false,
		};

		var cardInner = new VerticalStackLayout { Spacing = 6 };
		cardInner.Children.Add(cardGrid);
		cardInner.Children.Add(_noteBodyBorder);

		_normalCardBorder = new Border
		{
			Margin = new Thickness(6, 3),
			Padding = new Thickness(10),
			Background = bgBrush,
			Stroke = ruleBrush,
			StrokeThickness = 1,
			StrokeShape = new RoundRectangle { CornerRadius = 12 },
			Content = cardInner,
			IsVisible = false,
		};

		var rowStack = new VerticalStackLayout { Spacing = 0 };
		rowStack.Children.Add(_sectionBreakBorder);
		rowStack.Children.Add(_normalCardBorder);

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

		if (BindingContext is V2RowItem next)
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
			case nameof(V2RowItem.IsCurrent):
				ApplyCurrent();
				break;
			case nameof(V2RowItem.IsNoteOpen):
				ApplyNoteOpen();
				break;
			case nameof(V2RowItem.HasMemo):
				_memoDot.IsVisible = _item!.HasMemo;
				break;
			case nameof(V2RowItem.Marker):
			case nameof(V2RowItem.HasMarker):
			case nameof(V2RowItem.MarkerText):
			case nameof(V2RowItem.IsMarkerFlag):
			case nameof(V2RowItem.IsMarkerCaution):
			case nameof(V2RowItem.IsMarkerStar):
				ApplyMarker();
				break;
			case nameof(V2RowItem.CompactPlatformSize):
				_trackBorder.WidthRequest = _item!.CompactPlatformSize;
				_trackBorder.HeightRequest = _item.CompactPlatformSize;
				break;
			case nameof(V2RowItem.CompactPlatformFontSize):
				_trackLabel.FontSize = _item!.CompactPlatformFontSize;
				break;
			case nameof(V2RowItem.CompactTimeFontSize):
				_arriveLabel.FontSize = _item!.CompactTimeFontSize;
				_departLabel.FontSize = _item.CompactTimeFontSize;
				break;
			case nameof(V2RowItem.CompactStationFontSize):
				_stationNameLabel.FontSize = _item!.CompactStationFontSize;
				break;
			case nameof(V2RowItem.CompactCardMargin):
				_normalCardBorder.Margin = _item!.CompactCardMargin;
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
		_normalCardBorder.IsVisible   = !_item.IsSectionBreakRow;

		_sectionBreakLabel.Text = _item.SectionBreakLabel;

		_runLabel.Text         = _item.RunText;
		_stationNameLabel.Text = _item.StationName;
		_stationNameLabel.FontSize = _item.CompactStationFontSize;
		_arriveLabel.Text  = _item.ArriveText;
		_arriveLabel.FontSize = _item.CompactTimeFontSize;
		_departLabel.Text  = _item.DepartText;
		_departLabel.FontSize = _item.CompactTimeFontSize;
		_trackLabel.Text   = _item.TrackName;
		_trackLabel.FontSize = _item.CompactPlatformFontSize;
		_trackBorder.IsVisible = _item.HasTrackName;
		_trackBorder.WidthRequest = _item.CompactPlatformSize;
		_trackBorder.HeightRequest = _item.CompactPlatformSize;
		_normalCardBorder.Margin = _item.CompactCardMargin;

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

		var res = Application.Current?.Resources;
		if (_item.IsCurrent)
		{
			_normalCardBorder.Background = (Brush?)res?["OT_AccentSoft"]
				?? new SolidColorBrush(V2RowTablet.LookupColor("OT_AccentSoft_Light"));
			_normalCardBorder.Stroke = (Brush?)res?["OT_Accent"]
				?? new SolidColorBrush(V2RowTablet.LookupColor("OT_Accent_Light"));
			_normalCardBorder.StrokeThickness = 1.5;
			_stationNameLabel.TextColor = V2RowTablet.LookupColorThemeAware("OT_AccentFgStrong_Light", "OT_AccentFgStrong_Dark");
			_trackBorder.Background = (Brush?)res?["OT_Accent"]
				?? new SolidColorBrush(V2RowTablet.LookupColor("OT_Accent_Light"));
			_trackLabel.TextColor = V2RowTablet.LookupColorThemeAware("OT_AccentFg_Light", "OT_AccentFg_Dark");
		}
		else
		{
			_normalCardBorder.Background = (Brush?)res?["OT_Bg"]
				?? new SolidColorBrush(Colors.White);
			_normalCardBorder.Stroke = (Brush?)res?["OT_Rule"]
				?? new SolidColorBrush(Colors.Gray);
			_normalCardBorder.StrokeThickness = 1;
			_stationNameLabel.TextColor = _item.IsPass
				? V2RowTablet.LookupColorThemeAware("OT_Muted_Light", "OT_Muted_Dark")
				: V2RowTablet.LookupColorThemeAware("OT_Fg_Light", "OT_Fg_Dark");
			_trackBorder.Background = (Brush?)res?["OT_PlatBg"]
				?? new SolidColorBrush(Colors.LightGray);
			_trackLabel.TextColor = V2RowTablet.LookupColorThemeAware("OT_PlatFg_Light", "OT_PlatFg_Dark");
		}
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
				?? new SolidColorBrush(V2RowTablet.LookupColor("OT_MarkerFlagBg"));
			_markerBadgeLabel.TextColor = V2RowTablet.LookupColor("OT_MarkerFlagFg");
		}
		else if (_item.IsMarkerCaution)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerCautionBgBrush"]
				?? new SolidColorBrush(V2RowTablet.LookupColor("OT_MarkerCautionBg"));
			_markerBadgeLabel.TextColor = V2RowTablet.LookupColor("OT_MarkerCautionFg");
		}
		else if (_item.IsMarkerStar)
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_MarkerStarBgBrush"]
				?? new SolidColorBrush(V2RowTablet.LookupColor("OT_MarkerStarBg"));
			_markerBadgeLabel.TextColor = V2RowTablet.LookupColor("OT_MarkerStarFg");
		}
		else
		{
			_markerBadgeBorder.Background = (Brush?)res?["OT_Bg"]
				?? new SolidColorBrush(Colors.White);
			_markerBadgeLabel.TextColor = V2RowTablet.LookupColor("OT_MarkerFlagFg");
		}
	}

	void ApplyNoteOpen()
	{
		if (_item is null)
			return;
		_noteBodyBorder.IsVisible = _item.IsNoteOpen;
	}

	void InvokeOnPage(Action<OriginalTimetableV2Page> action)
	{
		Element? cur = this;
		while (cur is not null)
		{
			if (cur is OriginalTimetableV2Page page)
			{
				action(page);
				return;
			}
			cur = cur.Parent;
		}
	}
}

#endregion
