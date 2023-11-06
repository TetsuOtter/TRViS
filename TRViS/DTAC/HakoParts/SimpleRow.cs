using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.IO.Models;

namespace TRViS.DTAC.HakoParts;

public partial class SimpleRow
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	static readonly Thickness StaNameTrainNumButtonMargin = new(0, 4);
	static readonly Thickness TrainNumButtonPadding = new(32, 4);
	static readonly double TimeLabelFontSize_HHMM = DTACElementStyles.LargeTextSize * 0.8;
	static readonly double TimeLabelFontSize_SS = TimeLabelFontSize_HHMM * 0.7;
	static readonly double TrainNumberLabelTextSize = DTACElementStyles.LargeTextSize * 0.8;

	public event ValueChangedEventHandler<bool>? IsSelectedChanged;

	readonly Label FromStationNameLabel;
	readonly Label FromTimeLabel;
	readonly Label ToStationNameLabel;
	readonly Label ToTimeLabel;
	static Label GenStationNameLabel(int rowIndex, int colIndex)
	{
		Label v = DTACElementStyles.LargeLabelStyle<Label>();
		v.VerticalOptions = LayoutOptions.End;
		v.HorizontalOptions = LayoutOptions.Center;
		v.Margin = StaNameTrainNumButtonMargin;

		Grid.SetRow(v, rowIndex);
		Grid.SetColumn(v, colIndex);

		return v;
	}
	static Label GenTimeLabel(int rowIndex, int colIndex)
	{
		Label v = DTACElementStyles.LabelStyle<Label>();
		v.VerticalOptions = LayoutOptions.Center;
		v.HorizontalOptions = LayoutOptions.Center;

		Grid.SetRow(v, rowIndex);
		Grid.SetColumn(v, colIndex);

		return v;
	}

	readonly Frame SelectTrainButtonFrame;
	static readonly Color SelectTrainButtonFrameBorderColor = new(0.6f);
	static readonly Shadow SelectTrainButtonFrameShadow = new()
	{
		Brush = Colors.Black,
		Offset = new(0, 0),
		Radius = 4,
		Opacity = 0.4f,
	};
	static Frame GenSelectTrainButtonFrame(Label TrainNumberLabel)
	{
		Frame v = new()
		{
			Margin = StaNameTrainNumButtonMargin,
			Padding = TrainNumButtonPadding,
			VerticalOptions = LayoutOptions.End,
			HorizontalOptions = LayoutOptions.Center,
			MinimumWidthRequest = 120,
			HasShadow = true,
			Shadow = SelectTrainButtonFrameShadow,
			BorderColor = SelectTrainButtonFrameBorderColor,
		};
		DTACElementStyles.DefaultBGColor.Apply(v, Frame.BackgroundColorProperty);
		v.Content = TrainNumberLabel;

		return v;
	}

	readonly ToggleButton SelectTrainButton = new();
	static ToggleButton GenSelectTrainButton(Frame SelectTrainButtonFrame, EventHandler<ValueChangedEventArgs<bool>> IsSelectedChanged, int rowIndex)
	{
		ToggleButton v = new()
		{
			Content = SelectTrainButtonFrame
		};

		Grid.SetColumn(v, 1);
		Grid.SetRow(v, rowIndex);

		v.IsCheckedChanged += IsSelectedChanged;

		return v;
	}
	readonly Label TrainNumberLabel;
	static Label GenTrainNumberLabel()
	{
		Label v = DTACElementStyles.LargeLabelStyle<Label>();

		v.FontSize = TrainNumberLabelTextSize;

		return v;
	}

	readonly Line RouteLine = new()
	{
		StrokeThickness = 1,
		VerticalOptions = LayoutOptions.Center,
		HorizontalOptions = LayoutOptions.Fill,
	};
	static Line GenRouteLine(int rowIndex)
	{
		Line v = new()
		{
			StrokeThickness = 2,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Fill,
		};

		DTACElementStyles.ForegroundBlackWhite.Apply(v, Line.BackgroundColorProperty);

		Grid.SetColumn(v, 1);
		Grid.SetRow(v, rowIndex);

		return v;
	}

	readonly Grid ParentGrid;

	public SimpleRow(Grid parentGrid, int dataIndex)
	{
		logger.Debug("Creating");

		ParentGrid = parentGrid;

		int rowIndex_StaName_SelectBtn = dataIndex * 2;
		int rowIndex_time = rowIndex_StaName_SelectBtn + 1;

		TrainNumberLabel = GenTrainNumberLabel();
		SelectTrainButtonFrame = GenSelectTrainButtonFrame(TrainNumberLabel);
		SelectTrainButton = GenSelectTrainButton(SelectTrainButtonFrame, (sender, e) =>
		{
			IsSelectedChanged?.Invoke(this, e.OldValue, e.NewValue);
			SetTrainNumberButtonState();
		}, rowIndex_StaName_SelectBtn);

		RouteLine = GenRouteLine(rowIndex_time);

		FromStationNameLabel = GenStationNameLabel(rowIndex_StaName_SelectBtn, 0);
		ToStationNameLabel = GenStationNameLabel(rowIndex_StaName_SelectBtn, 2);

		FromTimeLabel = GenTimeLabel(rowIndex_time, 0);
		ToTimeLabel = GenTimeLabel(rowIndex_time, 2);

		parentGrid.Add(SelectTrainButton);
		parentGrid.Add(FromStationNameLabel);
		parentGrid.Add(ToStationNameLabel);

		parentGrid.Add(FromTimeLabel);
		parentGrid.Add(RouteLine);
		parentGrid.Add(ToTimeLabel);

		logger.Debug("Created");
	}

	void SetTrainNumberButtonState()
	{
		if (SelectTrainButton.IsEnabled && SelectTrainButton.IsChecked)
		{
			DTACElementStyles.DefaultGreen.Apply(SelectTrainButtonFrame, Frame.BorderColorProperty);
			SelectTrainButtonFrame.HasShadow = false;
		}
		else
		{
			SelectTrainButtonFrame.BorderColor = SelectTrainButtonFrameBorderColor;
			SelectTrainButtonFrame.HasShadow = true;
		}
	}

	public bool IsEnabled
	{
		get => SelectTrainButton.IsEnabled;
		set
		{
			SelectTrainButton.IsEnabled = value;
			SetTrainNumberButtonState();
		}
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
		set => TrainNumberLabel.Text = Utils.ToWide(value);
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

		double baseTextSize = TimeLabelFontSize_HHMM;
		fs.Spans.Add(new(){
			Text = $"{value.Hour:00}:{value.Minute:00}",
			FontAttributes = FontAttributes.Bold,
			FontSize = baseTextSize,
		});
		fs.Spans.Add(new(){
			Text = value.Second?.ToString("00"),
			FontSize = TimeLabelFontSize_SS,
		});

		return fs;
	}
}
