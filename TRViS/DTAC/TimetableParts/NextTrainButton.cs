using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC.TimetableParts;

public class NextTrainButton : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
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
			TrainData? nextTrainData;
			try
			{
				nextTrainData = InstanceManager.AppViewModel.Loader?.GetTrainData(value);
			}
			catch (Exception ex)
			{
				this.IsVisible = false;
				string msg = "Cannot get the timetable of the next train.\n"
					+ $"WorkGroupID: {InstanceManager.AppViewModel.SelectedWorkGroup?.Id}\n"
					+ $"WorkID: {InstanceManager.AppViewModel.SelectedWork?.Id}\n"
					+ $"TrainID: {InstanceManager.AppViewModel.SelectedTrainData?.Id}\n"
					+ $"CurrentNextTrainID: {_NextTrainId}\n"
					+ $"GivenNextTrainID: {value}";
				logger.Error(ex, msg);
				return;
			}
			if (nextTrainData is null)
			{
				throw new KeyNotFoundException($"Next TrainData not found (id: {value})");
			}
			else if (nextTrainData.TrainNumber is null)
			{
				throw new NullReferenceException($"Next TrainData has no TrainNumber (id: {value})");
			}

			_NextTrainId = value;
			this.IsVisible = true;

			string trainNumberToShow = TRViS.Core.StringUtils.InsertCharBetweenCharAndMakeWide(nextTrainData.TrainNumber, TRViS.Core.StringUtils.THIN_SPACE);
			_NextTrainButton.Text = $"{trainNumberToShow}の時刻表へ";
		}
	}

	private void NextTrainButton_Click(object? _, EventArgs e)
	{
		if (string.IsNullOrEmpty(_NextTrainId))
			return;

		try
		{
			InstanceManager.AppViewModel.SelectedTrainData = InstanceManager.AppViewModel.Loader?.GetTrainData(_NextTrainId);
		}
		catch (Exception ex)
		{
			string msg = "次の列車の時刻表を取得できませんでした。\n"
				+ $"WorkGroupID: {InstanceManager.AppViewModel.SelectedWorkGroup?.Id}\n"
				+ $"WorkID: {InstanceManager.AppViewModel.SelectedWork?.Id}\n"
				+ $"TrainID: {InstanceManager.AppViewModel.SelectedTrainData?.Id}\n"
				+ $"NextTrainID: {_NextTrainId}";
			logger.Error(ex, "Unknown Exception: " + msg);
			InstanceManager.CrashlyticsWrapper.Log(ex, "NextTrainButton.Click");
			Utils.DisplayAlert("エラー", msg, "OK");
		}
	}
}
