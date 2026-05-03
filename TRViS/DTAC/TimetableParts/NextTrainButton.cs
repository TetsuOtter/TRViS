using TRViS.DTAC.Logic.Presenter;

namespace TRViS.DTAC.TimetableParts;

public class NextTrainButton : Grid
{
	readonly Button _NextTrainButton = new()
	{
		FontFamily = DTACElementStyles.DefaultFontFamily,
		FontSize = DTACElementStyles.LargeTextSize,
		TextColor = Colors.White,
		FontAttributes = FontAttributes.Bold,
		Margin = new(32, 10),
		MinimumWidthRequest = 400,
		CornerRadius = 4,
		Shadow = DTACElementStyles.DefaultShadow,
		FontAutoScalingEnabled = false,
	};

	private readonly NextTrainButtonPresenter _presenter;

	public NextTrainButton()
	{
		_presenter = Adapters.PresenterFactory.BuildNextTrainButtonPresenter();
		_presenter.StateChanged += OnPresenterStateChanged;

		DTACElementStyles.SemiDarkGreen.Apply(_NextTrainButton, BackgroundColorProperty);
		_NextTrainButton.Clicked += NextTrainButton_Click;

		ColumnDefinitions.Add(new(new(1, GridUnitType.Star)));
		ColumnDefinitions.Add(new(new(4, GridUnitType.Star)));
		ColumnDefinitions.Add(new(new(1, GridUnitType.Star)));
		Grid.SetColumn(_NextTrainButton, 1);
		Children.Add(_NextTrainButton);
	}

	private void OnPresenterStateChanged(object? _, NextTrainButtonState state)
	{
		this.IsVisible = state.IsVisible;
		if (state.IsVisible)
		{
			_NextTrainButton.Text = state.ButtonText;
		}
	}

	private void NextTrainButton_Click(object? _, EventArgs e)
		=> _presenter.OnButtonClicked();
}
