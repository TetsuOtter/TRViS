using TRViS.Controls;
using TRViS.IO.Models;
using TRViS.ValueConverters.DTAC;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class VerticalTimetableRow
{
	public bool IsEnabled { get; set; } = true;

	bool _IsMarkingMode;
	public bool IsMarkingMode
	{
		get => _IsMarkingMode;
		private set
		{
			MarkerBox.IsVisible = value || (MarkedColor is not null);
			MarkerBox.IsEnabled = value;
			_IsMarkingMode = value;
		}
	}

	void setMarkerBoxDefaultColor()
		=> DTACElementStyles.MarkerMarkButtonBGColor.Apply(MarkerBox, VisualElement.BackgroundColorProperty);

	Color? _MarkedColor = null;
	public Color? MarkedColor
	{
		get => _MarkedColor;
		private set
		{
			if (_MarkedColor == value)
				return;

			if (value == Colors.Transparent)
				value = null;
			_MarkedColor = value;

			if (value is null)
			{
				BackgroundBoxView.Color = Colors.Transparent;
				setMarkerBoxDefaultColor();
			}
			else
			{
				BackgroundBoxView.Color = value;
				MarkerBox.BackgroundColor = value;
				MarkerBox.TextColor = Utils.GetTextColorFromBGColor(value);
			}
		}
	}
	public int RowIndex { get; }
	public TimetableRow RowData { get; }

	readonly DTACMarkerViewModel? MarkerViewModel;

	readonly Label? DriveTimeMM;
	readonly Label? DriveTimeSS;

	readonly BoxView BackgroundBoxView;
	readonly Button MarkerBox;

	public IList<IGestureRecognizer> GestureRecognizers => BackgroundBoxView.GestureRecognizers;

	public VerticalTimetableRow(Grid parent, int rowIndex, TimetableRow rowData, DTACMarkerViewModel? markerViewModel = null, bool isLastRow = false)
	{
		#region Init Props
		MarkerViewModel = markerViewModel;
		IsLastRow = isLastRow;
		RowIndex = rowIndex;
		RowData = rowData;

		if (MarkerViewModel is not null)
			MarkerViewModel.PropertyChanged += OnMarkerViewModelValueChanged;
		#endregion

		BackgroundBoxView = new()
		{
			IsVisible = true,
			Color = Colors.Transparent,
			BindingContext = this,
			Opacity = BG_ALPHA,
		};
		Grid.SetColumnSpan(BackgroundBoxView, 8);
		parent.Add(BackgroundBoxView, row: rowIndex);

		#region Drive Time
		bool isDriveTimeMMVisible = rowData.DriveTimeMM is not null and > 0;
		bool isDriveTimeSSVisible = rowData.DriveTimeMM is not null and > 0;

		if (isDriveTimeMMVisible || isDriveTimeSSVisible)
		{
			Grid DriveTimeGrid = new()
			{
				Margin = new(2, 0),
				VerticalOptions = LayoutOptions.End,
				ColumnDefinitions =
				{
					new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)),
					new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
				},
				InputTransparent = true,
				ZIndex = 2,
			};

			if (isDriveTimeMMVisible)
			{
				DriveTimeMM = DTACElementStyles.TimetableDriveTimeMMLabel<Label>();
				DriveTimeMM.Text = rowData.DriveTimeMM.ToString();
				DriveTimeGrid.Add(DriveTimeMM, column: 0);
			}

			if (isDriveTimeSSVisible)
			{
				DriveTimeSS = DTACElementStyles.TimetableDriveTimeSSLabel<Label>();
				DriveTimeSS.Text = rowData.DriveTimeSS.ToString();
				DriveTimeGrid.Add(DriveTimeSS, column: 1);
			}

			parent.Add(DriveTimeGrid, 0, rowIndex);
		}
		#endregion

		if (!string.IsNullOrEmpty(rowData.StationName))
		{
			HtmlAutoDetectLabel StationName = DTACElementStyles.TimetableLabel<HtmlAutoDetectLabel>();
			StationName.Margin = new(0);
			StationName.Text = StationNameConverter.Convert(rowData.StationName);
			parent.Add(StationName, 1, rowIndex);
		}

		#region Arrive / Departure Time
		if (rowData.ArriveTime is not null)
		{
			TimeCell ArriveTime = DTACElementStyles.TimeCell();
			ArriveTime.TimeData = rowData.ArriveTime;
			ArriveTime.IsPass = rowData.IsPass;
			parent.Add(ArriveTime, 2, rowIndex);
		}

		if (rowData.HasBracket)
		{
			Label OpenBracket = DTACElementStyles.TimetableLabel<Label>();
			OpenBracket.HorizontalOptions = LayoutOptions.Start;
			OpenBracket.Text = "(";
			parent.Add(OpenBracket, 2, rowIndex);

			Label CloseBracket = DTACElementStyles.TimetableLabel<Label>();
			CloseBracket.HorizontalOptions = LayoutOptions.End;
			CloseBracket.Text = ")";
			parent.Add(CloseBracket, 2, rowIndex);
		}

		if (rowData.DepartureTime is not null)
		{
			TimeCell DepartureTime = DTACElementStyles.TimeCell();
			DepartureTime.TimeData = rowData.DepartureTime;
			DepartureTime.IsPass = rowData.IsPass;
			parent.Add(DepartureTime, 3, rowIndex);
		}

		if (rowData.IsLastStop)
		{
			Grid LastStopLine = DTACElementStyles.LastStopLineGrid();

			parent.Add(LastStopLine, 3, rowIndex);
		}

		if (rowData.IsOperationOnlyStop)
		{
			Label OpOnlyStopOpenBracket = DTACElementStyles.TimetableLabel<Label>();
			OpOnlyStopOpenBracket.HorizontalOptions = LayoutOptions.Start;
			OpOnlyStopOpenBracket.Text = "[";
			parent.Add(OpOnlyStopOpenBracket, 2, rowIndex);

			Label OpOnlyStopCloseBracket = DTACElementStyles.TimetableLabel<Label>();
			OpOnlyStopCloseBracket.HorizontalOptions = LayoutOptions.End;
			OpOnlyStopCloseBracket.Text = "]";
			parent.Add(OpOnlyStopCloseBracket, 3, rowIndex);
		}
		#endregion

		if (!string.IsNullOrEmpty(rowData.TrackName))
		{
			HtmlAutoDetectLabel TrackName = DTACElementStyles.TimetableLabel<HtmlAutoDetectLabel>();
			TrackName.Margin = TrackName.Padding = new(0);
			TrackName.HorizontalOptions = TrackName.VerticalOptions = LayoutOptions.Center;
			TrackName.TextColor = Colors.Red;
			TrackName.Text = rowData.TrackName;
			parent.Add(TrackName, 4, rowIndex);
		}

		#region RunIn / RunOut Limit
		bool isRunInLimitVisible = rowData.RunInLimit is not null and > 0;
		bool isRunOutLimitVisible = rowData.RunOutLimit is not null and > 0;

		if (isRunInLimitVisible || isRunOutLimitVisible)
		{
			Grid RunInOutLimitGrid = new()
			{
				Margin = new(10, 4),
				Padding = new(0),
				RowDefinitions =
				{
					new RowDefinition(new GridLength(1, GridUnitType.Star)),
					new RowDefinition(new GridLength(1, GridUnitType.Star)),
				},
				InputTransparent = true,
			};

			if (isRunInLimitVisible)
			{
				Label RunInLimit = DTACElementStyles.TimetableRunLimitLabel<Label>();
				RunInLimit.HorizontalOptions = LayoutOptions.Start;
				RunInLimit.Text = rowData.RunInLimit.ToString();
				RunInOutLimitGrid.Add(RunInLimit, row: 0);
			}

			if (isRunOutLimitVisible)
			{
				Label RunOutLimit = DTACElementStyles.TimetableRunLimitLabel<Label>();
				RunOutLimit.HorizontalOptions = LayoutOptions.End;
				RunOutLimit.Text = rowData.RunOutLimit.ToString();
				RunInOutLimitGrid.Add(RunOutLimit, row: 1);
			}

			parent.Add(RunInOutLimitGrid, 5, rowIndex);
		}
		#endregion

		if (!string.IsNullOrEmpty(rowData.Remarks))
		{
			HtmlAutoDetectLabel Remarks = DTACElementStyles.TimetableLabel<HtmlAutoDetectLabel>();
			Remarks.FontAttributes = FontAttributes.None;
			Remarks.HorizontalOptions = LayoutOptions.Start;
			Remarks.FontSize = 16;
			Remarks.Text = rowData.Remarks;
			parent.Add(Remarks, 6, rowIndex);
		}

		parent.Add(DTACElementStyles.HorizontalSeparatorLineStyle(), row: rowIndex);

		MarkerBox = new()
		{
			IsVisible = false,
			IsEnabled = false,
			FontFamily = "Hiragino Sans",
			FontSize = 18,
			BorderColor = Colors.Transparent,
			CornerRadius = 4,
			Padding = new(0),
			Margin = new(8),
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Center,
			HeightRequest = 40,
			WidthRequest = 40,
			Shadow = DTACElementStyles.DefaultShadow,
			Opacity = 0.9,
		};
		setMarkerBoxDefaultColor();
		MarkerBox.Shadow.Offset = new(2, 2);
		MarkerBox.Shadow.Radius = 2;
		MarkerBox.Clicked += MarkerBoxClicked;

		parent.Add(MarkerBox, 8, rowIndex);
	}
}

