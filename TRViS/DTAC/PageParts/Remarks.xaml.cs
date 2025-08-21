using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<IHasRemarksProperty>("RemarksData", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<GridLength>("ContentAreaHeight", IsReadOnly = true)]
[DependencyProperty<double>("BottomSafeAreaHeight")]
public partial class Remarks : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public const double HEADER_HEIGHT = 64;
	const double DEFAULT_CONTENT_AREA_HEIGHT = 160;
	double BottomMargin
		=> -ContentAreaHeight.Value - BottomSafeAreaHeight;

	public Remarks()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		BindingContext = this;

		ContentAreaHeight = new(DEFAULT_CONTENT_AREA_HEIGHT);

		DTACElementStyles.DefaultBGColor.Apply(RemarksTextScrollView, BackgroundColorProperty);
		RemarksLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;

		logger.Trace("Created");
	}

	public async void ResetTextScrollViewPosition(bool? isOpen = null)
	{
		try
		{
			isOpen ??= IsOpen;
			Task? remarksTextScrollViewTranslateTask = null;
#if IOS
			if (Shell.Current is AppShell shell)
			{
				double translateToY = isOpen.Value ? 0 : shell.SafeAreaMargin.Bottom;
				logger.Trace("translateToY: {0} (isOpen: {1})", translateToY, isOpen.Value);
				remarksTextScrollViewTranslateTask = RemarksTextScrollView.TranslateToAsync(
					x: 0,
					y: translateToY,
					length: 250 / 2,
					easing: Easing.CubicOut
				);
			}
#endif
			Task remarksTranslateTask = this.TranslateToAsync(0, isOpen.Value ? BottomMargin : 0, easing: Easing.SinInOut);
			if (remarksTextScrollViewTranslateTask is not null)
			{
				await Task.WhenAll(remarksTranslateTask, remarksTextScrollViewTranslateTask);
			}
			else
			{
				await remarksTranslateTask;
			}
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "Remarks.ResetTextScrollViewPosition");
			await Utils.ExitWithAlert(ex);
		}
	}

	partial void OnIsOpenChanged(bool newValue)
	{
		logger.Info("IsOpen: {0}, BottomMargin: {1}", newValue, BottomMargin);
		OpenCloseButton.IsOpen = newValue;
		ResetTextScrollViewPosition(newValue);
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
		try
		{
			logger.Trace("width: {0}, height: {1}", width, height);
			OnPageHeightChanged(Shell.Current.CurrentPage.Height * 0.25);

			base.OnSizeAllocated(width, height);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "Remarks.OnSizeAllocated");
			Utils.ExitWithAlert(ex);
		}
	}

	void OpenCloseButton_IsOpenChanged(object sender, ValueChangedEventArgs<bool> e)
	{
		try
		{
			logger.Info("OpenCloseButton.IsOpen: {0}", e.NewValue);
			this.IsOpen = e.NewValue;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "Remarks.OpenCloseButton_IsOpenChanged");
			Utils.ExitWithAlert(ex);
		}
	}
}
