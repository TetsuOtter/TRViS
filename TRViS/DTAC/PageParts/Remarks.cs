using DependencyPropertyGenerator;

using TRViS.Controls;
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

	private readonly Label titleLabel;
	private readonly OpenCloseButton openCloseButton;
	private readonly ScrollView remarksTextScrollView;
	private readonly HtmlAutoDetectLabel remarksLabel;

	public Remarks()
	{
		logger.Trace("Creating...");

		BackgroundColor = Color.FromArgb("#333");
		HeightRequest = 320;
		VerticalOptions = LayoutOptions.End;

		RowDefinitions.Add(new RowDefinition(64));
		RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		titleLabel = new Label
		{
			FontSize = 28,
			FontFamily = "Hiragino Sans W6",
			FontAutoScalingEnabled = false,
			Margin = new Thickness(16, 0),
			Text = "注 意 事 項",
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Center,
			TextColor = Colors.White
		};
		Grid.SetRow(titleLabel, 0);

		openCloseButton = new OpenCloseButton
		{
			TextWhenOpen = "\ue5cf",
			TextWhenClosed = "\ue5ce",
			Margin = new Thickness(16, 0),
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center
		};
		openCloseButton.IsOpenChanged += OpenCloseButton_IsOpenChanged;
		Grid.SetRow(openCloseButton, 0);

		remarksLabel = DTACElementStyles.Instance.HtmlAutoDetectLabelStyle<HtmlAutoDetectLabel>();
		remarksLabel.FontAutoScalingEnabled = true;
		remarksLabel.HorizontalOptions = LayoutOptions.Start;
		remarksLabel.VerticalOptions = LayoutOptions.Start;

		remarksTextScrollView = new ScrollView
		{
			Padding = new Thickness(2),
			Margin = new Thickness(8, 0, 8, 8),
			Content = remarksLabel
		};
		DTACElementStyles.Instance.DefaultBGColor.Apply(remarksTextScrollView, BackgroundColorProperty);
		Grid.SetRow(remarksTextScrollView, 1);

		Children.Add(titleLabel);
		Children.Add(openCloseButton);
		Children.Add(remarksTextScrollView);

		BindingContext = this;

		ContentAreaHeight = new(DEFAULT_CONTENT_AREA_HEIGHT);

		DTACElementStyles.Instance.DefaultBGColor.Apply(remarksTextScrollView, BackgroundColorProperty);
		remarksLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.Instance.DefaultTextColor;

		logger.Trace("Created");
	}

	public void ResetTextScrollViewPosition(bool? isOpen = null)
	{
		try
		{
			isOpen ??= IsOpen;
#if IOS
			if (Shell.Current is AppShell shell)
			{
				double translateToY = isOpen.Value ? 0 : shell.SafeAreaMargin.Bottom;
				logger.Trace("translateToY: {0} (isOpen: {1})", translateToY, isOpen.Value);
				_ = remarksTextScrollView.TranslateToAsync(
					x: 0,
					y: translateToY,
					length: 250 / 2,
					easing: Easing.CubicOut
				);
			}
#endif
			_ = this.TranslateToAsync(0, isOpen.Value ? BottomMargin : 0, easing: Easing.SinInOut);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "Remarks.ResetTextScrollViewPosition");
			Utils.ExitWithAlert(ex);
		}
	}

	partial void OnIsOpenChanged(bool newValue)
	{
		logger.Info("IsOpen: {0}, BottomMargin: {1}", newValue, BottomMargin);
		openCloseButton.IsOpen = newValue;
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

		remarksLabel.Text = newValue?.Remarks;
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

	void OpenCloseButton_IsOpenChanged(object? sender, ValueChangedEventArgs<bool> e)
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
