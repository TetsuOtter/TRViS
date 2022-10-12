using DependencyPropertyGenerator;
using Microsoft.Maui;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using TRViS.IO.Models;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<IHasRemarksProperty>("RemarksData", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<GridLength>("BottomPadding", IsReadOnly = true)]
public partial class Remarks : Grid
{
	double BottomMargin
		=> -256 - BottomPadding.Value;

	public Remarks()
	{
		InitializeComponent();

		BindingContext = this;

		Margin = new(0, BottomMargin);
		BottomPadding = new(0);
	}

	partial void OnIsOpenChanged(bool newValue)
		=> this.TranslateTo(0, newValue ? BottomMargin : 0, easing: Easing.SinInOut);

	partial void OnBottomPaddingChanged(GridLength newValue)
	{
		Margin = new(0, BottomMargin);
		HeightRequest = 320 + newValue.Value;
	}

	partial void OnRemarksDataChanged(IHasRemarksProperty? newValue)
		=> RemarksLabel.Text = newValue?.Remarks;

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

		base.OnSizeAllocated(width, height);
	}
#endif
}
