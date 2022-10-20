using DependencyPropertyGenerator;
using Microsoft.Maui;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using TRViS.IO.Models;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<IHasRemarksProperty>("RemarksData", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<GridLength>("ContentAreaHeight", IsReadOnly = true)]
[DependencyProperty<GridLength>("BottomPadding", IsReadOnly = true)]
public partial class Remarks : Grid
{
	public const double HEADER_HEIGHT = 64;
	const double DEFAULT_CONTENT_AREA_HEIGHT = 256;
	double BottomMargin
		=> -ContentAreaHeight.Value - BottomPadding.Value;

	public Remarks()
	{
		InitializeComponent();

		BindingContext = this;

		ContentAreaHeight = new(DEFAULT_CONTENT_AREA_HEIGHT);
		BottomPadding = new(0);
	}

	partial void OnIsOpenChanged(bool newValue)
		=> this.TranslateTo(0, newValue ? BottomMargin : 0, easing: Easing.SinInOut);

	partial void OnBottomPaddingChanged(GridLength newValue)
		=> HeightChanged(ContentAreaHeight.Value, newValue.Value);

	partial void OnContentAreaHeightChanged(GridLength newValue)
		=> HeightChanged(newValue.Value, BottomPadding.Value);

	partial void OnRemarksDataChanged(IHasRemarksProperty? newValue)
		=> RemarksLabel.Text = newValue?.Remarks;

	void HeightChanged(in double contentAreaHeight, in double bottomPadding)
	{
		Margin = new(0, BottomMargin);
		HeightRequest = HEADER_HEIGHT + contentAreaHeight + bottomPadding;
		OnIsOpenChanged(IsOpen);
	}

	void OnPageHeightChanged(in double newValue)
	{
		ContentAreaHeight = new(newValue switch
		{
			<= 64 => 64,
			>= DEFAULT_CONTENT_AREA_HEIGHT => DEFAULT_CONTENT_AREA_HEIGHT,
			_ => newValue
		});
	}

#if IOS
	UIKit.UIWindow? UIWindow = null;

	protected override void OnSizeAllocated(double width, double height)
	{
		// SafeAreaInsets ref: https://stackoverflow.com/questions/46829840/get-safe-area-inset-top-and-bottom-heights
		// ios15 >= ref: https://zenn.dev/paraches/articles/windows_was_depricated_in_ios15
		if (UIWindow is null)
		{
			if (UIKit.UIDevice.CurrentDevice.CheckSystemVersion(15, 0))
			{
				if (UIKit.UIApplication.SharedApplication.ConnectedScenes.ToArray().FirstOrDefault(v => v is UIKit.UIWindowScene) is UIKit.UIWindowScene scene)
					UIWindow = scene.Windows.FirstOrDefault();
			}
			else
				UIWindow = UIKit.UIApplication.SharedApplication.Windows.FirstOrDefault();
		}

		if (UIWindow is not null)
			BottomPadding = new(UIWindow.SafeAreaInsets.Bottom.Value);

		OnPageHeightChanged(Shell.Current.CurrentPage.Height * 0.4);

		base.OnSizeAllocated(width, height);
	}
#else
	protected override void OnSizeAllocated(double width, double height)
	{
		OnPageHeightChanged(Shell.Current.CurrentPage.Height * 0.4);

		base.OnSizeAllocated(width, height);
	}
#endif
}
