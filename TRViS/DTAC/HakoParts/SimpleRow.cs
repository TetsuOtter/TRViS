using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.IO.Models;

namespace TRViS.DTAC.HakoParts;

public partial class SimpleRow
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	public event ValueChangedEventHandler<bool>? IsSelectedChanged;

	readonly Label FromStationNameLabel = DTACElementStyles.LargeLabelStyle<Label>();
	readonly Label FromTimeLabel = DTACElementStyles.LabelStyle<Label>();
	readonly Label ToStationNameLabel = DTACElementStyles.LargeLabelStyle<Label>();
	readonly Label ToTimeLabel = DTACElementStyles.LabelStyle<Label>();

	readonly Frame SelectTrainButtonFrame = new();
	readonly ToggleButton SelectTrainButton = new();
	readonly Label TrainNumberLabel = DTACElementStyles.LargeLabelStyle<Label>();

	readonly Line RouteLine = new()
	{
		StrokeThickness = 1,
		VerticalOptions = LayoutOptions.Center,
		HorizontalOptions = LayoutOptions.Fill,
	};

	readonly Grid ParentGrid;

	public SimpleRow(Grid parentGrid, int dataIndex)
	{
		logger.Debug("Creating");

		ParentGrid = parentGrid;

		SelectTrainButton.IsCheckedChanged += (sender, e) =>
		{
			IsSelectedChanged?.Invoke(this, e.OldValue, e.NewValue);
		};
		SelectTrainButton.Content = SelectTrainButtonFrame;
		DTACElementStyles.DefaultBGColor.Apply(SelectTrainButtonFrame, Frame.BackgroundColorProperty);
		SelectTrainButtonFrame.Content = TrainNumberLabel;

		DTACElementStyles.ForegroundBlackWhite.Apply(RouteLine, Line.BackgroundColorProperty);

		int rowIndex_StaName_SelectBtn = dataIndex * 2;
		int rowIndex_time = rowIndex_StaName_SelectBtn + 1;
		Grid.SetRow(FromStationNameLabel, rowIndex_StaName_SelectBtn);
		Grid.SetRow(SelectTrainButton, rowIndex_StaName_SelectBtn);
		Grid.SetRow(ToStationNameLabel, rowIndex_StaName_SelectBtn);

		Grid.SetRow(FromTimeLabel, rowIndex_time);
		Grid.SetRow(RouteLine, rowIndex_time);
		Grid.SetRow(ToTimeLabel, rowIndex_time);

		Grid.SetColumn(FromStationNameLabel, 0);
		Grid.SetColumn(SelectTrainButton, 1);
		Grid.SetColumn(ToStationNameLabel, 2);

		Grid.SetColumn(FromTimeLabel, 0);
		Grid.SetColumn(RouteLine, 1);
		Grid.SetColumn(ToTimeLabel, 2);

		parentGrid.Add(FromStationNameLabel);
		parentGrid.Add(SelectTrainButton);
		parentGrid.Add(ToStationNameLabel);

		parentGrid.Add(FromTimeLabel);
		parentGrid.Add(RouteLine);
		parentGrid.Add(ToTimeLabel);

		logger.Debug("Created");
	}

	public string FromStationName
	{
		get => FromStationNameLabel.Text;
		set => FromStationNameLabel.Text = value;
	}
	public string ToStationName
	{
		get => ToStationNameLabel.Text;
		set => ToStationNameLabel.Text = value;
	}
	public string TrainNumber
	{
		get => TrainNumberLabel.Text;
		set => TrainNumberLabel.Text = value;
	}

	private TimeData? fromTime;
	public TimeData? FromTime
	{
		get => fromTime;
		set
		{
			if (value == fromTime)
				return;
			fromTime = value;
			if (value is null)
				FromTimeLabel.Text = null;
			else
				FromTimeLabel.FormattedText = GetTimeLabelText(value);
		}
	}
	private TimeData? toTime;
	public TimeData? ToTime
	{
		get => toTime;
		set
		{
			if (value == toTime)
				return;
			toTime = value;
			if (value is null)
				ToTimeLabel.Text = null;
			else
				ToTimeLabel.FormattedText = GetTimeLabelText(value);
		}
	}

	static FormattedString GetTimeLabelText(TimeData value)
	{
		FormattedString fs = new();

		double baseTextSize = DTACElementStyles.LargeTextSize * 0.8;
		fs.Spans.Add(new(){
			Text = $"{value.Hour:00}:{value.Minute:00}",
			FontAttributes = FontAttributes.Bold,
			FontSize = baseTextSize,
		});
		fs.Spans.Add(new(){
			Text = value.Second?.ToString("00"),
			FontSize = baseTextSize * 0.75,
		});

		return fs;
	}
}
