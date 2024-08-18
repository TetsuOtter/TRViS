using TRViS.IO.Models;
using TRViS.ValueConverters.DTAC;

namespace TRViS.DTAC.TimetableParts;

public class NextTrainButton : Grid
{
	readonly Button _NextTrainButton = new()
	{
		FontFamily = DTACElementStyles.DefaultFontFamily,
		FontSize = DTACElementStyles.LargeTextSize,
		TextColor = Colors.White,
		FontAttributes = FontAttributes.Bold,
		Margin = new(32, 8),
		MinimumWidthRequest = 400,
		CornerRadius = 4,
		Shadow = DTACElementStyles.DefaultShadow,
		FontAutoScalingEnabled = false,
	};

	public NextTrainButton()
	{
		DTACElementStyles.SemiDarkGreen.Apply(_NextTrainButton, BackgroundColorProperty);
		_NextTrainButton.Clicked += NextTrainButton_Click;

		ColumnDefinitions.Add(new(new(1, GridUnitType.Star)));
		ColumnDefinitions.Add(new(new(4, GridUnitType.Star)));
		ColumnDefinitions.Add(new(new(1, GridUnitType.Star)));
		Grid.SetColumn(_NextTrainButton, 1);
		Children.Add(_NextTrainButton);
	}

	private string _NextTrainId = string.Empty;
	public string NextTrainId
	{
		get => _NextTrainId;
		set
		{
			if (_NextTrainId == value)
				return;

			TrainData? nextTrainData = InstanceManager.AppViewModel.Loader?.GetTrainData(value);
			if (nextTrainData is null)
			{
				throw new KeyNotFoundException($"Next TrainData not found (id: {value})");
			}
			else if (nextTrainData.TrainNumber is null)
			{
				throw new NullReferenceException($"Next TrainData has no TrainNumber (id: {value})");
			}

			_NextTrainId = value;

			string trainNumberToShow = Utils.InsertCharBetweenCharAndMakeWide(nextTrainData.TrainNumber, Utils.THIN_SPACE);
			_NextTrainButton.Text = $"{trainNumberToShow}の時刻表へ";
		}
	}

	private void NextTrainButton_Click(object? _, EventArgs e)
	{
		if (string.IsNullOrEmpty(_NextTrainId))
			return;

		InstanceManager.AppViewModel.SelectedTrainData = InstanceManager.AppViewModel.Loader?.GetTrainData(_NextTrainId);
	}
}
