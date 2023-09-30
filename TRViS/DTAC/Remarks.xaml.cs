using DependencyPropertyGenerator;
using TRViS.IO.Models;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay, IsReadOnly = true)]
[DependencyProperty<IHasRemarksProperty>("RemarksData", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<GridLength>("ContentAreaHeight", IsReadOnly = true)]
[DependencyProperty<double>("BottomSafeAreaHeight")]
public partial class Remarks : Grid
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public const double HEADER_HEIGHT = 64;
	const double DEFAULT_CONTENT_AREA_HEIGHT = 256;
	double BottomMargin
		=> -ContentAreaHeight.Value - BottomSafeAreaHeight;

	public Remarks()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		BindingContext = this;

		ContentAreaHeight = new(DEFAULT_CONTENT_AREA_HEIGHT);

		DTACElementStyles.DefaultBGColor.Apply(RemarksTextScrollView, BackgroundColorProperty);
		logger.Trace("Created");
	}

	partial void OnIsOpenChanged(bool newValue)
	{
		logger.Info("IsOpen: {0}, BottomMargin: {1}", newValue, BottomMargin);
		this.TranslateTo(0, newValue ? BottomMargin : 0, easing: Easing.SinInOut);
	}

	partial void OnBottomSafeAreaHeightChanged(double newValue)
	{
		logger.Trace("newValue: {0}", newValue);
		Margin = new(0, BottomMargin);
	}

	partial void OnContentAreaHeightChanged(GridLength newValue)
		=> HeightChanged(newValue.Value);

	partial void OnRemarksDataChanged(IHasRemarksProperty? newValue)
	{
		if (newValue is null || string.IsNullOrEmpty(newValue.Remarks))
		{
			logger.Warn("newValue is null or Remarks is null or empty");
		}
		else
		{
			logger.Info("newValue: {0}", newValue.Remarks);
		}
		
		RemarksLabel.Text = newValue?.Remarks;
	}

	void HeightChanged(in double contentAreaHeight)
	{
		logger.Trace("contentAreaHeight: {0}", contentAreaHeight);

		Margin = new(0, BottomMargin);
		HeightRequest = HEADER_HEIGHT + contentAreaHeight;
		logger.Trace("HeightRequest: {0}", HeightRequest);

		OnIsOpenChanged(IsOpen);
	}

	void OnPageHeightChanged(in double newValue)
	{
		logger.Trace("newValue: {0}", newValue);

		ContentAreaHeight = new(newValue switch
		{
			<= 64 => 64,
			>= DEFAULT_CONTENT_AREA_HEIGHT => DEFAULT_CONTENT_AREA_HEIGHT,
			_ => newValue
		});

		logger.Trace("ContentAreaHeight: {0}", ContentAreaHeight);
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		logger.Trace("width: {0}, height: {1}", width, height);
		OnPageHeightChanged(Shell.Current.CurrentPage.Height * 0.4);

		base.OnSizeAllocated(width, height);
	}

	void OpenCloseButton_IsOpenChanged(object sender, ValueChangedEventArgs<bool> e)
	{
		logger.Info("OpenCloseButton.IsOpen: {0}", e.NewValue);
		this.IsOpen = e.NewValue;
	}
}
