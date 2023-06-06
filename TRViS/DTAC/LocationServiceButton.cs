using TRViS.Controls;

namespace TRViS.DTAC;

public class LocationServiceButton : ToggleButton
{
	const float CornerRadius = 5;
	const float SelectedRectMargin = 2;
	const float SelectedRectThickness = 2;
	const float NotSelectedRectMargin = 1;

	readonly Label Label_Location = DTACElementStyles.LargeLabelStyle<Label>();
	readonly Label Label_ON = DTACElementStyles.LabelStyle<Label>();
	readonly Label Label_OFF = DTACElementStyles.LabelStyle<Label>();

	readonly Frame SelectedSideBase = new()
	{
		Margin = new(SelectedRectMargin),
		Padding = new(0),
		CornerRadius = CornerRadius - SelectedRectMargin,
		BackgroundColor = Colors.White,
		BorderColor = Colors.Transparent,
		HasShadow = false,
		Content = new BoxView()
		{
			Margin = new(SelectedRectThickness),
			CornerRadius = CornerRadius - SelectedRectMargin - SelectedRectThickness,
		}
	};
	readonly Frame NotSelectedSideBase = new()
	{
		Margin = new(NotSelectedRectMargin),
		CornerRadius = CornerRadius - NotSelectedRectMargin,
		BackgroundColor = Colors.White,
		Shadow = DTACElementStyles.DefaultShadow,
	};

	public LocationServiceButton()
	{
		IsCheckedChanged += OnIsCheckedChanged;

		Grid grid = new()
		{
			ColumnDefinitions = new()
			{
				new ColumnDefinition(new(1, GridUnitType.Star)),
				new ColumnDefinition(new(1, GridUnitType.Star))
			}
		};

		Frame baseFrame = new()
		{
			Margin = new(0),
			Padding = new(0),
			CornerRadius = CornerRadius,
			BorderColor = Colors.Transparent,
			HasShadow = true,
			Shadow = DTACElementStyles.DefaultShadow,
		};
		Grid.SetColumnSpan(baseFrame, 2);

		InitElements();

		DTACElementStyles.DarkGreen.Apply(baseFrame, BackgroundColorProperty);
		DTACElementStyles.DarkGreen.Apply(SelectedSideBase.Content, BoxView.ColorProperty);

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

		grid.Add(baseFrame, 0);
		grid.Add(SelectedSideBase, 0);
		grid.Add(NotSelectedSideBase, 0);

		grid.Add(on_group, 0);
		grid.Add(Label_OFF, 1);

		Content = grid;

		OnIsCheckedChanged(false);
	}

	void InitElements()
	{
		Label_Location.Text = "\xe0c8";
		Label_ON.Text = "ON";
		Label_OFF.Text = "OFF";

		Label_Location.FontAttributes
			= Label_ON.FontAttributes
			= Label_OFF.FontAttributes
			= FontAttributes.Bold;

		Label_ON.FontSize
			= Label_OFF.FontSize
			= DTACElementStyles.DefaultTextSize + 2;

		Label_Location.FontFamily = DTACElementStyles.MaterialIconFontFamily;

		Label_Location.Margin
			= Label_ON.Margin
			= Label_OFF.Margin
			= Label_Location.Padding
			= Label_ON.Padding
			= Label_OFF.Padding
			= new(0);
	}

	void OnIsCheckedChanged(object? sender, ValueChangedEventArgs<bool> e)
		=> OnIsCheckedChanged(e.NewValue);

	void OnIsCheckedChanged(bool isLocationServiceEnabled)
	{
		Label_ON.TextColor
			= Label_Location.TextColor
			= isLocationServiceEnabled ? Colors.White : Colors.Black;
		Label_OFF.TextColor
			= !isLocationServiceEnabled ? Colors.White : Colors.Black;

		Grid.SetColumn(SelectedSideBase, isLocationServiceEnabled ? 0 : 1);
		Grid.SetColumn(NotSelectedSideBase, !isLocationServiceEnabled ? 0 : 1);
	}
}
