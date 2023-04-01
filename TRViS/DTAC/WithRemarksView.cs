using DependencyPropertyGenerator;
using TRViS.IO.Models;

namespace TRViS.DTAC;

[ContentProperty(nameof(Content))]
[DependencyProperty<View>("Content")]
[DependencyProperty<IHasRemarksProperty>("RemarksData")]
public partial class WithRemarksView : Grid
{
	Remarks RemarksView { get; } = new();
	RowDefinition RemarksAreaRowDefinition { get; } = new(new(Remarks.HEADER_HEIGHT, GridUnitType.Absolute));

#if IOS
	BoxView BottomPaddingView { get; } = new()
	{
		Color = new(0x33, 0x33, 0x33),
	};
#endif

	public WithRemarksView()
	{
		RowDefinitions.Add(new(new(1, GridUnitType.Star)));
		RowDefinitions.Add(RemarksAreaRowDefinition);

		IgnoreSafeArea = true;
		Margin = new(0);
		Padding = new(0);

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

#if IOS
		this.Add(BottomPaddingView, row: 1);
#endif

		this.Add(RemarksView, row: 1);
	}

	partial void OnContentChanged(View? oldValue, View? newValue)
	{
		if (oldValue is not null)
			this.Remove(oldValue);
		if (newValue is not null)
			this.Insert(0, newValue);
	}

	partial void OnRemarksDataChanged(IHasRemarksProperty? newValue)
	{
		RemarksView.RemarksData = newValue;
	}

	private void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
#if IOS
		double bottomPaddingValue = newValue.Bottom;

		if (oldValue.Bottom == bottomPaddingValue)
			return;

		if (bottomPaddingValue > 0)
		{
			BottomPaddingView.IsVisible = true;
			BottomPaddingView.Margin = new(0, 0, 0, -bottomPaddingValue);
		}
		else
		{
			BottomPaddingView.IsVisible = false;
		}

		RemarksAreaRowDefinition.Height = Remarks.HEADER_HEIGHT - bottomPaddingValue;
		RemarksView.BottomSafeAreaHeight = bottomPaddingValue;
#endif
	}
}

