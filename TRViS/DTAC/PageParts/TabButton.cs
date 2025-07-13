using System.ComponentModel;
using System.Runtime.CompilerServices;

using DependencyPropertyGenerator;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<DTACViewHostViewModel.Mode>("CurrentMode", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<DTACViewHostViewModel.Mode>("TargetMode")]
[DependencyProperty<bool>("IsSelected", IsReadOnly = true)]
[DependencyProperty<string>("Text")]
public partial class TabButton : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public static readonly Color BASE_COLOR_DISABLED = new(0xDD, 0xDD, 0xDD);

	public static readonly double NORMAL_MODE_WIDTH = 152;

	private readonly Grid rootGrid;
	private readonly Grid innerGrid;
	private readonly BoxView baseBox;
	private readonly Label buttonLabel;
	private readonly Line bottomLine;

	public TabButton()
	{
		logger.Trace("Creating...");

		WidthRequest = 152;

		rootGrid = new Grid
		{
			Margin = new Thickness(8, 0)
		};
		rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
		rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

		innerGrid = new Grid();
		Grid.SetRow(innerGrid, 1);

		baseBox = new BoxView
		{
			Color = BASE_COLOR_DISABLED,
			CornerRadius = 4,
			Margin = new Thickness(0, -4),
			Shadow = new Shadow
			{
				Brush = Brush.Black,
				Offset = new Point(2, 2),
				Radius = 2,
				Opacity = 0
			}
		};

		buttonLabel = new Label
		{
			FontFamily = "Hiragino Sans",
			FontSize = 18,
			FontAutoScalingEnabled = false,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center
		};

		bottomLine = new Line
		{
			IsVisible = false,
			HeightRequest = 4,
			VerticalOptions = LayoutOptions.End,
			BackgroundColor = Color.FromArgb("#080"),
			Fill = Brush.Green,
			Margin = new Thickness(8, 0)
		};
		Grid.SetRow(bottomLine, 1);

		var tapGestureRecognizer = new TapGestureRecognizer();
		tapGestureRecognizer.Tapped += BaseBox_Tapped;
		innerGrid.GestureRecognizers.Add(tapGestureRecognizer);

		innerGrid.Children.Add(baseBox);
		innerGrid.Children.Add(buttonLabel);
		innerGrid.Children.Add(bottomLine);
		rootGrid.Children.Add(innerGrid);

		Content = rootGrid;

		UpdateIsSelectedProperty();
		DTACElementStyles.Instance.TimetableTextColor.Apply(buttonLabel, Label.TextColorProperty);

		InstanceManager.AppViewModel.PropertyChanged += AppViewModel_PropertyChanged;
		OnWindowWidthChanged(InstanceManager.AppViewModel.WindowWidth);

		OnIsEnabledChanged(IsEnabled);

		logger.Trace("Created");
	}

	private void AppViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(AppViewModel.WindowWidth):
				OnWindowWidthChanged(InstanceManager.AppViewModel.WindowWidth);
				break;
		}
	}
	protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);
		switch (propertyName)
		{
			case nameof(IsEnabled):
				OnIsEnabledChanged(IsEnabled);
				break;
		}
	}
	void OnWindowWidthChanged(double newValue)
	{
		if (newValue == 0)
		{
			return;
		}
		try
		{
			logger.Trace("newValue: {0}", newValue);

			int tabButtonCount = 3;
			double calcedMaxWidth = (newValue - 8) / tabButtonCount;
			double widthRequestValue = Math.Min(calcedMaxWidth, NORMAL_MODE_WIDTH);
			logger.Trace("OnWindowWidthChanged WidthRequest newValue: {0}", widthRequestValue);
			WidthRequest = widthRequestValue;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "TabButton.OnWindowWidthChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	partial void OnCurrentModeChanged()
	{
		logger.Trace("CurrentMode: {0}", CurrentMode);
		UpdateIsSelectedProperty();
	}
	partial void OnTargetModeChanged()
	{
		logger.Trace("TargetMode: {0}", TargetMode);
		UpdateIsSelectedProperty();
	}

	void UpdateIsSelectedProperty()
	{
		IsSelected = (CurrentMode == TargetMode);
		logger.Info("CurrentMode: {0}, TargetMode: {1}, IsSelected: {2}", CurrentMode, TargetMode, IsSelected);
	}

	partial void OnTextChanged(string? newValue)
	{
		logger.Trace("newValue: {0}", newValue);
		buttonLabel.Text = newValue;
	}

	partial void OnIsSelectedChanged(bool newValue)
	{
		logger.Trace("newValue: {0}", newValue);
		bottomLine.IsVisible = newValue;

		if (newValue)
		{
			DTACElementStyles.Instance.DefaultBGColor.Apply(baseBox, BoxView.ColorProperty);
			baseBox.Shadow.Opacity = 0.2f;
			logger.Info("Tab `{0}` selected", Text);
		}
		else
		{
			DTACElementStyles.Instance.TabButtonBGColor.Apply(baseBox, BoxView.ColorProperty);
			baseBox.Shadow.Opacity = 0;
			logger.Info("Tab `{0}` unselected", Text);
		}
	}

	private void OnIsEnabledChanged(bool newValue)
	{
		if (newValue)
		{
			buttonLabel.Opacity = 1;
		}
		else
		{
			buttonLabel.Opacity = 0.5;
			if (IsSelected)
			{
				InstanceManager.DTACViewHostViewModel.TabMode = DTACViewHostViewModel.Mode.Hako;
			}
		}
	}

	void BaseBox_Tapped(object? sender, TappedEventArgs e)
	{
		try
		{
			if (IsSelected || !IsEnabled)
			{
				logger.Info("Tapped `{0}` but... IsSelected: {1}, IsEnabled: {2}", Text, IsSelected, IsEnabled);
				return;
			}

			logger.Info("Tapped `{0}`", Text);
			CurrentMode = TargetMode;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "TabButton.BaseBox_Tapped");
			Utils.ExitWithAlert(ex);
		}
	}
}
