using CommunityToolkit.Maui.Views;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class MarkerButton : Border
{
	DTACMarkerViewModel MarkerSettings { get; }
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private readonly Grid contentGrid;
	private readonly Label markerLabel;
	private readonly Label iconLabel;

	public MarkerButton()
	{
		logger.Trace("Creating...");

		MarkerSettings = InstanceManager.DTACMarkerViewModel;
		BindingContext = MarkerSettings;

		Padding = new Thickness(0);
		WidthRequest = 56;
		HeightRequest = 44;
		HorizontalOptions = LayoutOptions.Center;
		VerticalOptions = LayoutOptions.Center;
		Stroke = Brush.Transparent;
		DTACElementStyles.Instance.OpenCloseButtonBGColor.Apply(this, BackgroundColorProperty);
		StrokeShape = new RoundRectangle { CornerRadius = 4 };

		contentGrid = new Grid
		{
#if IOS || MACCATALYST
			Margin = new Thickness(4, 6)
#else
			Margin = new Thickness(4, 0)
#endif
		};

		markerLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		markerLabel.HorizontalOptions = LayoutOptions.Start;
		markerLabel.VerticalOptions = LayoutOptions.Start;
		markerLabel.FontAttributes = FontAttributes.Bold;
		markerLabel.Text = "ﾏｰｶｰ";

		iconLabel = DTACElementStyles.Instance.HeaderLabelStyle<Label>();
		iconLabel.HorizontalOptions = LayoutOptions.End;
		iconLabel.VerticalOptions = LayoutOptions.Center;
		iconLabel.FontFamily = "MaterialIconsRegular";
		iconLabel.FontSize = 44;
		iconLabel.ScaleX = 0.5;
		iconLabel.AnchorX = 1;
		iconLabel.Text = "\ue9a2";
		DTACElementStyles.Instance.MarkerButtonIconColor.Apply(iconLabel, Label.TextColorProperty);

		contentGrid.Children.Add(markerLabel);
		contentGrid.Children.Add(iconLabel);

		Content = contentGrid;

		var tapGestureRecognizer = new TapGestureRecognizer();
		tapGestureRecognizer.Tapped += TapGestureRecognizer_Tapped;
		GestureRecognizers.Add(tapGestureRecognizer);

		MarkerSettings.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(DTACMarkerViewModel.IsToggled))
			{
				UpdateAppearanceForToggleState();
			}
		};

		UpdateAppearanceForToggleState();

		logger.Trace("Created");
	}

	private void UpdateAppearanceForToggleState()
	{
		if (MarkerSettings.IsToggled)
		{
			BackgroundColor = DTACElementStyles.Instance.DarkerGreen;
			markerLabel.TextColor = Colors.White;
			iconLabel.TextColor = Colors.White;
		}
		else
		{
			BackgroundColor = null;
			markerLabel.TextColor = null;
			iconLabel.TextColor = null;
			DTACElementStyles.Instance.OpenCloseButtonBGColor.Apply(this, BackgroundColorProperty);
			DTACElementStyles.Instance.HeaderTextColor.Apply(markerLabel, Label.TextColorProperty);
			DTACElementStyles.Instance.MarkerButtonIconColor.Apply(iconLabel, Label.TextColorProperty);
		}
	}

	async void TapGestureRecognizer_Tapped(object? sender, TappedEventArgs e)
	{
		try
		{
			if (Shell.Current.CurrentPage is not ViewHost page)
			{
				logger.Warn("Shell.Current.CurrentPage is not ViewHost");
				return;
			}

			if (MarkerSettings.IsToggled)
			{
				logger.Info("MarkerSettings.IsToggled set true -> false");
				MarkerSettings.IsToggled = false;
				return;
			}

			MarkerSettings.IsToggled = true;

			SelectMarkerPopup popup = new(MarkerSettings)
			{
				Anchor = this,
			};

			logger.Info("Showing SelectMarkerPopup");
			await page.ShowPopupAsync(popup);
			logger.Trace("SelectMarkerPopup Shown");
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "MarkerButton.Tap");
			await Utils.ExitWithAlert(ex);
		}
	}
}
