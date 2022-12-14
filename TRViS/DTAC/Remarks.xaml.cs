using DependencyPropertyGenerator;
using Microsoft.Maui;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using TRViS.IO.Models;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<IHasRemarksProperty>("RemarksData", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<GridLength>("ContentAreaHeight", IsReadOnly = true)]
[DependencyProperty<double>("BottomSafeAreaHeight")]
public partial class Remarks : Grid
{
	public const double HEADER_HEIGHT = 64;
	const double DEFAULT_CONTENT_AREA_HEIGHT = 256;
	double BottomMargin
		=> -ContentAreaHeight.Value - BottomSafeAreaHeight;

	public Remarks()
	{
		InitializeComponent();

		BindingContext = this;

		ContentAreaHeight = new(DEFAULT_CONTENT_AREA_HEIGHT);
	}

	partial void OnIsOpenChanged(bool newValue)
		=> this.TranslateTo(0, newValue ? BottomMargin : 0, easing: Easing.SinInOut);

	partial void OnBottomSafeAreaHeightChanged(double newValue)
		=> Margin = new(0, BottomMargin);

	partial void OnContentAreaHeightChanged(GridLength newValue)
		=> HeightChanged(newValue.Value);

	partial void OnRemarksDataChanged(IHasRemarksProperty? newValue)
		=> RemarksLabel.Text = newValue?.Remarks;

	void HeightChanged(in double contentAreaHeight)
	{
		Margin = new(0, BottomMargin);
		HeightRequest = HEADER_HEIGHT + contentAreaHeight;
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

	protected override void OnSizeAllocated(double width, double height)
	{
		OnPageHeightChanged(Shell.Current.CurrentPage.Height * 0.4);

		base.OnSizeAllocated(width, height);
	}
}
