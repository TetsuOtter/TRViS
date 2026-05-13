using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.Services;
using TRViS.Utils;

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
		AutomationId = "DTAC.NextTrainButton";

		_presenter = Adapters.PresenterFactory.BuildNextTrainButtonPresenter();
		_presenter.StateChanged += OnPresenterStateChanged;
		OnPresenterStateChanged(null, _presenter.CurrentState);

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

	/// <summary>
	/// Asks the Presenter to re-evaluate its state from the latest AppViewModel.
	/// The Presenter remains the source of truth for NextTrainId; this method
	/// only signals "lifecycle event happened, please re-evaluate".
	/// </summary>
	public void Refresh() => _presenter.Refresh();

	private void NextTrainButton_Click(object? _, EventArgs e)
	{
		try
		{
			_presenter.OnButtonClicked();
		}
		catch (UserAlertException ex)
		{
			Util.DisplayAlertAsync(ex.Title, ex.Message, ex.CancelLabel);
		}
	}
}
