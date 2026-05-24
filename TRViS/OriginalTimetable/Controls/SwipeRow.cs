using System.Collections;
using System.Collections.ObjectModel;

namespace TRViS.OriginalTimetable.Controls;

// 行のラッパー。MAUI SwipeView を使い、Leading/Trailing アクションを expose する。
// HIG の direction-lock / threshold 等は SwipeView 側で処理されるため自前パンを書かない。
public class SwipeRow : ContentView
{
	public static readonly BindableProperty LeadingActionsProperty = BindableProperty.Create(
		nameof(LeadingActions), typeof(IList<View>), typeof(SwipeRow), null,
		propertyChanged: OnActionsChanged);
	public static readonly BindableProperty TrailingActionsProperty = BindableProperty.Create(
		nameof(TrailingActions), typeof(IList<View>), typeof(SwipeRow), null,
		propertyChanged: OnActionsChanged);
	public static readonly BindableProperty RowContentProperty = BindableProperty.Create(
		nameof(RowContent), typeof(View), typeof(SwipeRow), null,
		propertyChanged: OnRowContentChanged);

	// readonly な OneTime バインディングだと初期化順で空 List が漏れるので
	// 直接 set できる ObservableCollection をデフォルトに置く。
	public IList<View> LeadingActions
	{
		get => (IList<View>)GetValue(LeadingActionsProperty) ?? (IList<View>)(GetValue(LeadingActionsProperty) ?? new ObservableCollection<View>());
		set => SetValue(LeadingActionsProperty, value);
	}
	public IList<View> TrailingActions
	{
		get => (IList<View>)GetValue(TrailingActionsProperty) ?? (IList<View>)(GetValue(TrailingActionsProperty) ?? new ObservableCollection<View>());
		set => SetValue(TrailingActionsProperty, value);
	}
	public View? RowContent
	{
		get => (View?)GetValue(RowContentProperty);
		set => SetValue(RowContentProperty, value);
	}

	public event EventHandler? Tapped;
	public event EventHandler? LongPressed;

	private readonly SwipeView _swipe;
	private readonly Grid _hostGrid;

	public SwipeRow()
	{
		_hostGrid = new Grid();
		_swipe = new SwipeView
		{
			Threshold = 80,
			Content = _hostGrid,
		};

		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, e) => Tapped?.Invoke(this, EventArgs.Empty);
		_hostGrid.GestureRecognizers.Add(tap);

#if IOS || ANDROID
		// PointerGestureRecognizer doesn't fire long-press; use a TapGesture +
		// platform-specific behavior would be needed. Here we keep it simple:
		// a SwipeGesture toward the actions OR a sustained press isn't trivial
		// in MAUI cross-platform — so we expose LongPressed but only wire it on
		// platforms that have a built-in. TODO(next slice): plumb a real
		// long-press via platform handlers.
#endif

		Content = _swipe;
	}

	private static void OnRowContentChanged(BindableObject b, object oldVal, object newVal)
	{
		if (b is not SwipeRow r) return;
		r._hostGrid.Children.Clear();
		if (newVal is View v)
			r._hostGrid.Children.Add(v);
	}

	private static void OnActionsChanged(BindableObject b, object oldVal, object newVal)
	{
		if (b is not SwipeRow r) return;
		r.RebuildSwipeItems();
	}

	private void RebuildSwipeItems()
	{
		if (LeadingActions is { Count: > 0 } leading)
		{
			var items = new SwipeItems { Mode = SwipeMode.Reveal };
			foreach (var view in leading)
				items.Add(WrapAsSwipeItem(view));
			_swipe.LeftItems = items;
		}
		else
		{
			_swipe.LeftItems = null;
		}

		if (TrailingActions is { Count: > 0 } trailing)
		{
			var items = new SwipeItems { Mode = SwipeMode.Reveal };
			foreach (var view in trailing)
				items.Add(WrapAsSwipeItem(view));
			_swipe.RightItems = items;
		}
		else
		{
			_swipe.RightItems = null;
		}
	}

	// SwipeItem を直接渡せる場合はそのまま、ContentView/Button を渡された場合は
	// SwipeItemView ラッパーで包む。
	private static Microsoft.Maui.Controls.ISwipeItem WrapAsSwipeItem(View view)
	{
		if (view is Microsoft.Maui.Controls.ISwipeItem item)
			return item;
		return new SwipeItemView { Content = view, WidthRequest = 88 };
	}

	internal void RaiseLongPressed() => LongPressed?.Invoke(this, EventArgs.Empty);
}
