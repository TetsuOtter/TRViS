using System.Runtime.CompilerServices;

using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.Services;

namespace TRViS.DTAC;

public class LocationServiceButton : ToggleButton
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	const float CornerRadius = 5;
	const float SelectedRectMargin = 2;
	const float SelectedRectThickness = 1;
	const float NotSelectedRectMargin = 1;

	readonly Label Label_Location = DTACElementStyles.Instance.LargeLabelStyle<Label>();
	readonly Label Label_ON = DTACElementStyles.Instance.LabelStyle<Label>();
	readonly Label Label_OFF = DTACElementStyles.Instance.LabelStyle<Label>();

	readonly Border SelectedSideBase = new()
	{
		Margin = new(SelectedRectMargin),
		Padding = new(0),
		Stroke = Colors.Transparent,
		StrokeShape = new RoundRectangle()
		{
			CornerRadius = new(CornerRadius - SelectedRectMargin),
		},
		Content = new BoxView()
		{
			Margin = new(SelectedRectThickness),
			CornerRadius = CornerRadius - SelectedRectMargin - SelectedRectThickness,
		}
	};
	readonly Border NotSelectedSideBase = new()
	{
		Margin = new(NotSelectedRectMargin),
		StrokeShape = new RoundRectangle()
		{
			CornerRadius = new(CornerRadius - NotSelectedRectMargin),
		},
		Shadow = DTACElementStyles.Instance.DefaultShadow,
	};

	public LocationServiceButton()
	{
		logger.Trace("Creating...");

		IsCheckedChanged += OnIsCheckedChanged;

		Grid grid = new()
		{
			ColumnDefinitions = new()
			{
				new ColumnDefinition(new(1, GridUnitType.Star)),
				new ColumnDefinition(new(1, GridUnitType.Star))
			}
		};

		Border baseBorder = new()
		{
			Margin = new(0),
			Padding = new(0),
			Stroke = Colors.Transparent,
			StrokeShape = new RoundRectangle()
			{
				CornerRadius = new(CornerRadius),
			},
			Shadow = DTACElementStyles.Instance.DefaultShadow,
		};

		InitElements();

		DTACElementStyles.Instance.DarkGreen.Apply(baseBorder, BackgroundColorProperty);
		DTACElementStyles.Instance.DarkGreen.Apply(SelectedSideBase.Content, BoxView.ColorProperty);
		DTACElementStyles.Instance.LocationServiceSelectedSideBorderColor.Apply(SelectedSideBase, BackgroundColorProperty);
		DTACElementStyles.Instance.LocationServiceNotSelectedSideBaseColor.Apply(NotSelectedSideBase, BackgroundColorProperty);

		HorizontalStackLayout on_group = new()
		{
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Margin = new(0),
			Padding = new(0),
		};

		on_group.Add(Label_Location);
		on_group.Add(Label_ON);

		on_group.ScaleX
			= Label_OFF.ScaleX
			= 0.9;

		Grid.SetColumnSpan(baseBorder, 2);
		grid.Add(baseBorder);
		grid.Add(SelectedSideBase);
		grid.Add(NotSelectedSideBase);

		grid.Add(on_group, 0);
		grid.Add(Label_OFF, 1);

		Content = grid;

		OnIsCheckedChanged(false);

		logger.Trace("Created");
	}

	void InitElements()
	{
		logger.Trace("Initializing elements...");

		Label_Location.Text = "\xe0c8";
		Label_ON.Text = "ON";
		Label_OFF.Text = "OFF";

		Label_Location.FontAttributes
			= Label_ON.FontAttributes
			= Label_OFF.FontAttributes
			= FontAttributes.Bold;

		Label_ON.FontSize
			= Label_OFF.FontSize
			= DTACElementStyles.Instance.DefaultTextSize + 2;

		Label_Location.FontFamily = DTACElementStyles.MaterialIconFontFamily;

		Label_Location.Margin
			= Label_ON.Margin
			= Label_OFF.Margin
			= Label_Location.Padding
			= Label_ON.Padding
			= Label_OFF.Padding
			= new(0);

		logger.Trace("Initialized");
	}

	void OnIsCheckedChanged(object? sender, ValueChangedEventArgs<bool> e)
		=> OnIsCheckedChanged(e.NewValue);

	void OnIsCheckedChanged(bool isLocationServiceEnabled)
	{
		try
		{
			if (!MainThread.IsMainThread)
			{
				logger.Debug("MainThread is not current thread -> invoke OnIsCheckedChanged on MainThread");
				MainThread.BeginInvokeOnMainThread(() => OnIsCheckedChanged(isLocationServiceEnabled));
				return;
			}

			if (isLocationServiceEnabled)
			{
				logger.Info("Location Service is enabled");
				DTACElementStyles.Instance.LocationServiceSelectedSideTextColor.Apply(Label_ON, Label.TextColorProperty);
				DTACElementStyles.Instance.LocationServiceSelectedSideTextColor.Apply(Label_Location, Label.TextColorProperty);
				DTACElementStyles.Instance.LocationServiceNotSelectedSideTextColor.Apply(Label_OFF, Label.TextColorProperty);
			}
			else
			{
				logger.Info("Location Service is disabled");
				DTACElementStyles.Instance.LocationServiceSelectedSideTextColor.Apply(Label_OFF, Label.TextColorProperty);
				DTACElementStyles.Instance.LocationServiceNotSelectedSideTextColor.Apply(Label_ON, Label.TextColorProperty);
				DTACElementStyles.Instance.LocationServiceNotSelectedSideTextColor.Apply(Label_Location, Label.TextColorProperty);
			}

			Grid.SetColumn(SelectedSideBase, isLocationServiceEnabled ? 0 : 1);
			Grid.SetColumn(NotSelectedSideBase, !isLocationServiceEnabled ? 0 : 1);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "LocationServiceButton.OnIsCheckedChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);

		if (propertyName == nameof(IsEnabled))
		{
			try
			{
				OnIsEnabledChanged(IsEnabled);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "LocationServiceButton.OnPropertyChanged");
				Utils.ExitWithAlert(ex);
			}
		}
	}
	void OnIsEnabledChanged(bool newValue)
	{
		logger.Trace("IsEnabled: {0}", newValue);
		if (newValue)
		{
			DTACElementStyles.Instance.LocationServiceSelectedSideBorderColor.Apply(SelectedSideBase, BackgroundColorProperty);
			DTACElementStyles.Instance.LocationServiceNotSelectedSideBaseColor.Apply(NotSelectedSideBase, BackgroundColorProperty);
		}
		else
		{
			DTACElementStyles.Instance.LocationServiceSelectedSideDisabledBorderColor.Apply(SelectedSideBase, BackgroundColorProperty);
			DTACElementStyles.Instance.LocationServiceNotSelectedSideDisabledBaseColor.Apply(NotSelectedSideBase, BackgroundColorProperty);
		}
	}
}
