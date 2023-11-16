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
	static Label GenStationNameLabel(int rowIndex, int colIndex, TimetableRow? row)
	{
		Label v = DTACElementStyles.LargeLabelStyle<Label>();
		v.VerticalOptions = LayoutOptions.End;
		v.HorizontalOptions = LayoutOptions.Center;
		v.Margin = StaNameTrainNumButtonMargin;
		v.Text = row?.StationName;

		Grid.SetRow(v, rowIndex);
		Grid.SetColumn(v, colIndex);

		return v;
	}
	static Label GenTimeLabel(int rowIndex, int colIndex, TimeData? time)
	{
		Label v = DTACElementStyles.LabelStyle<Label>();
		v.VerticalOptions = LayoutOptions.Center;
		v.HorizontalOptions = LayoutOptions.Center;

		if (time is not null && time.Hour is not null && time.Minute is not null)
		{
			v.FormattedText = GetTimeLabelText(time);
		}

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
			Content = SelectTrainButtonFrame,
		};

		Grid.SetColumn(v, 1);
		Grid.SetRow(v, rowIndex);

		v.IsCheckedChanged += IsSelectedChanged;

		return v;
	}
	readonly Label TrainNumberLabel;
	static Label GenTrainNumberLabel(TrainData trainData)
	{
		Label v = DTACElementStyles.LargeLabelStyle<Label>();

		v.FontSize = TrainNumberLabelTextSize;
		v.Text = trainData.TrainNumber;

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

	public TrainData TrainData { get; }
	public IO.Models.DB.TrainData DBTrainData { get; }
	public TimetableRow? FirstRow { get; } = null;
	public TimetableRow? LastRow { get; } = null;

	readonly Grid ParentGrid;

	public SimpleRow(Grid parentGrid, int dataIndex, TrainData TrainData, IO.Models.DB.TrainData DBTrainData)
	{
		logger.Debug("Creating");

		this.TrainData = TrainData;
		this.DBTrainData = DBTrainData;
		if (TrainData.Rows is not null)
		{
			FirstRow = TrainData.Rows.FirstOrDefault(v => !v.IsInfoRow);
			LastRow = TrainData.Rows.LastOrDefault(v => !v.IsInfoRow);
		}

		ParentGrid = parentGrid;

		int rowIndex_StaName_SelectBtn = dataIndex * 2;
		int rowIndex_time = rowIndex_StaName_SelectBtn + 1;

		TrainNumberLabel = GenTrainNumberLabel(TrainData);
		SelectTrainButtonFrame = GenSelectTrainButtonFrame(TrainNumberLabel);
		SelectTrainButton = GenSelectTrainButton(SelectTrainButtonFrame, (sender, e) =>
		{
			IsSelectedChanged?.Invoke(this, e.OldValue, e.NewValue);
			SetTrainNumberButtonState();
		}, rowIndex_StaName_SelectBtn);

		RouteLine = GenRouteLine(rowIndex_time);

		FromStationNameLabel = GenStationNameLabel(rowIndex_StaName_SelectBtn, 0, FirstRow);
		ToStationNameLabel = GenStationNameLabel(rowIndex_StaName_SelectBtn, 2, LastRow);

		FromTimeLabel = GenTimeLabel(rowIndex_time, 0, FirstRow?.DepartureTime);
		ToTimeLabel = GenTimeLabel(rowIndex_time, 2, LastRow?.ArriveTime);

		parentGrid.Add(SelectTrainButton);
		parentGrid.Add(FromStationNameLabel);
		parentGrid.Add(ToStationNameLabel);

		parentGrid.Add(FromTimeLabel);
		parentGrid.Add(RouteLine);
		parentGrid.Add(ToTimeLabel);

		SelectTrainButton.IsChecked = InstanceManager.AppViewModel.SelectedDBTrainData?.Id == DBTrainData.Id;
		SetTrainNumberButtonState();

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

	public bool IsSelected
	{
		get => SelectTrainButton.IsChecked;
		set => SelectTrainButton.IsChecked = value;
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

	public string FromStationName => FromStationNameLabel.Text;
	public string ToStationName => ToStationNameLabel.Text;
	public string TrainNumber => TrainNumberLabel.Text;

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
