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

#if IOS
	UIKit.UIWindow? UIWindow = null;

	protected override void OnSizeAllocated(double width, double height)
	{
		// SafeAreaInsets ref: https://stackoverflow.com/questions/46829840/get-safe-area-inset-top-and-bottom-heights
		// ios15 >= ref: https://zenn.dev/paraches/articles/windows_was_depricated_in_ios15
		if (UIWindow is null)
		{
			if (OperatingSystem.IsIOSVersionAtLeast(13, 0))
			{
				if (UIKit.UIApplication.SharedApplication.ConnectedScenes.ToArray().FirstOrDefault(v => v is UIKit.UIWindowScene) is UIKit.UIWindowScene scene)
					UIWindow = scene.Windows.FirstOrDefault();
			}
			else
				UIWindow = UIKit.UIApplication.SharedApplication.Windows.FirstOrDefault();
		}

		double bottomPaddingValue = 0;

		if (UIWindow is not null)
		{
			bottomPaddingValue = UIWindow.SafeAreaInsets.Bottom.Value;
		}

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

		base.OnSizeAllocated(width, height);
	}
#endif
}

