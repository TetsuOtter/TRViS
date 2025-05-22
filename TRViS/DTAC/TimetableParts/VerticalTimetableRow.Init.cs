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
			_IsMarkingMode = value;
			logger.Trace("IsMarkingMode: {0}, IsVisible: {1}, IsEnabled: {2}", value, MarkerBox.IsVisible, MarkerBox.IsEnabled);
		}
	}

	void setMarkerBoxDefaultColor()
		=> DTACElementStyles.MarkerMarkButtonBGColorBrush.Apply(MarkerBox, VisualElement.BackgroundProperty);

	Color? _MarkedColor = null;
	public Color? MarkedColor
	{
		get => _MarkedColor;
		private set
		{
			if (_MarkedColor == value)
			{
				logger.Debug("MarkedColor is not changed.");
				return;
			}

			if (value == Colors.Transparent)
			{
				logger.Debug("newValue is Transparent, so set value to null.");
				value = null;
			}
			_MarkedColor = value;

			if (value is null)
			{
				logger.Debug("MarkedColor is null, so set default color to MarkerBox.");
				BackgroundBoxView.Color = Colors.Transparent;
				setMarkerBoxDefaultColor();
			}
			else
			{
				BackgroundBoxView.Color = value;
				MarkerBox.Background = new SolidColorBrush(value);
				MarkerBox.TextColor = Utils.GetTextColorFromBGColor(value);
				logger.Debug("MarkedColor is not null, so set new color to MarkerBox. (Color: {0}, TextColor: {1})", value, MarkerBox.TextColor);
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
		logger.Debug("Creating VerticalTimetableRow (RowIndex: {0}, RowData: {1}, MarkerViewModel: {2}, IsLastRow: {3})",
			rowIndex,
			rowData,
			markerViewModel,
			isLastRow
		);

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
			ZIndex = DTACElementStyles.TimetableRowMarkerBackgroundZIndex,
		};
		Grid.SetRow(BackgroundBoxView, rowIndex);
		Grid.SetColumnSpan(BackgroundBoxView, 8);
		parent.Add(BackgroundBoxView);

		#region Drive Time
		bool isDriveTimeMMVisible = rowData.DriveTimeMM is not null and >= 0 and < 100;
		bool isDriveTimeSSVisible = rowData.DriveTimeSS is not null and >= 0 and < 100;
		logger.Debug("DriveTimeMM ({0}) -> Visible: {1}, DriveTimeSS ({2}) Visible: {3}",
			rowData.DriveTimeMM,
			isDriveTimeMMVisible,
			rowData.DriveTimeSS,
			isDriveTimeSSVisible
		);

		if (isDriveTimeMMVisible || isDriveTimeSSVisible)
		{
			logger.Trace("Creating DriveTimeGrid");
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
				ZIndex = DTACElementStyles.TimetableRowRunTimeTextZIndex,
			};

			if (isDriveTimeMMVisible)
			{
				logger.Trace("Creating DriveTimeMM");
				DriveTimeMM = DTACElementStyles.TimetableDriveTimeMMLabel<Label>();
				DriveTimeMM.Text = rowData.DriveTimeMM.ToString();
				DriveTimeGrid.Add(DriveTimeMM, column: 0);
			}

			if (isDriveTimeSSVisible)
			{
				logger.Trace("Creating DriveTimeSS");
				DriveTimeSS = DTACElementStyles.TimetableDriveTimeSSLabel<Label>();
				string? text = rowData.DriveTimeSS.ToString();
				if (text is not null and { Length: 1 })
				{
					logger.Trace("DriveTimeSS is 1 digit, so add space before text.");
					text = "  " + text;
				}
				DriveTimeSS.Text = text;
				DriveTimeGrid.Add(DriveTimeSS, column: 1);
			}

			parent.Add(DriveTimeGrid, 0, rowIndex);
		}
		#endregion

		if (!string.IsNullOrEmpty(rowData.StationName))
		{
			logger.Debug("Creating StationName");
			HtmlAutoDetectLabel StationName = DTACElementStyles.TimetableHtmlAutoDetectLabel<HtmlAutoDetectLabel>();
			StationName.Margin = new(0);
			StationName.Text = StationNameConverter.Convert(rowData.StationName);
			parent.Add(StationName, 1, rowIndex);
		}
		else
		{
			logger.Debug("StationName is null or empty, so skipping...");
		}

		#region Arrive / Departure Time
		if (rowData.ArriveTime is not null)
		{
			logger.Debug("Creating ArriveTime");
			TimeCell ArriveTime = DTACElementStyles.TimeCell();
			ArriveTime.TimeData = rowData.ArriveTime;
			ArriveTime.IsPass = rowData.IsPass;
			parent.Add(ArriveTime, 2, rowIndex);
		}
		{
			logger.Debug("ArruveTime is null, so skipping...");
		}

		if (rowData.HasBracket)
		{
			logger.Debug("Creating Bracket");
			Label OpenBracket = DTACElementStyles.TimetableLabel<Label>();
			OpenBracket.HorizontalOptions = LayoutOptions.Start;
			OpenBracket.Text = "(";
			parent.Add(OpenBracket, 2, rowIndex);

			logger.Trace("Creating CloseBracket");
			Label CloseBracket = DTACElementStyles.TimetableLabel<Label>();
			CloseBracket.HorizontalOptions = LayoutOptions.End;
			CloseBracket.Text = ")";
			parent.Add(CloseBracket, 2, rowIndex);
		}
		else
		{
			logger.Debug("HasBracket is false, so skipping...");
		}

		if (rowData.DepartureTime is not null)
		{
			logger.Debug("Creating DepartureTime");
			TimeCell DepartureTime = DTACElementStyles.TimeCell();
			DepartureTime.TimeData = rowData.DepartureTime;
			DepartureTime.IsPass = rowData.IsPass;
			parent.Add(DepartureTime, 3, rowIndex);
		}
		else
		{
			logger.Debug("DepartureTime is null, so skipping...");
		}

		if (rowData.IsLastStop)
		{
			logger.Debug("Creating LastStopLine");
			Grid LastStopLine = DTACElementStyles.LastStopLineGrid();

			parent.Add(LastStopLine, 3, rowIndex);
		}
		else
		{
			logger.Debug("IsLastStop is false, so skipping...");
		}

		if (rowData.IsOperationOnlyStop)
		{
			logger.Debug("Creating OperationOnlyStop Bracket");
			Label OpOnlyStopOpenBracket = DTACElementStyles.TimetableLabel<Label>();
			OpOnlyStopOpenBracket.HorizontalOptions = LayoutOptions.Start;
			OpOnlyStopOpenBracket.Text = "[";
			parent.Add(OpOnlyStopOpenBracket, 2, rowIndex);

			logger.Trace("Creating OperationOnlyStop CloseBracket");
			Label OpOnlyStopCloseBracket = DTACElementStyles.TimetableLabel<Label>();
			OpOnlyStopCloseBracket.HorizontalOptions = LayoutOptions.End;
			OpOnlyStopCloseBracket.Text = "]";
			parent.Add(OpOnlyStopCloseBracket, 3, rowIndex);
		}
		else
		{
			logger.Debug("IsOperationOnlyStop is false, so skipping...");
		}
		#endregion

		if (!string.IsNullOrEmpty(rowData.TrackName))
		{
			logger.Debug("Creating TrackName");
			HtmlAutoDetectLabel TrackName = DTACElementStyles.TimetableHtmlAutoDetectLabel<HtmlAutoDetectLabel>();
			TrackName.Margin = TrackName.Padding = new(0);
			TrackName.HorizontalOptions = TrackName.VerticalOptions = LayoutOptions.Center;
			TrackName.HorizontalTextAlignment = TextAlignment.Center;
			TrackName.TextColor = Colors.Red;
			TrackName.CurrentAppThemeColorBindingExtension = null;
			TrackName.FontSize = DTACElementStyles.GetTimetableTrackLabelFontSize(rowData.TrackName, TrackName.FontSize);
			TrackName.Text = rowData.TrackName;
			parent.Add(TrackName, 4, rowIndex);
		}
		else
		{
			logger.Debug("TrackName is null or empty, so skipping...");
		}

		#region RunIn / RunOut Limit
		bool isRunInLimitVisible = rowData.RunInLimit is not null and > 0;
		bool isRunOutLimitVisible = rowData.RunOutLimit is not null and > 0;
		logger.Debug("RunInLimit ({0}) -> Visible: {1}, RunOutLimit ({2}) Visible: {3}",
			rowData.RunInLimit,
			isRunInLimitVisible,
			rowData.RunOutLimit,
			isRunOutLimitVisible
		);

		if (isRunInLimitVisible || isRunOutLimitVisible)
		{
			logger.Trace("Creating RunInOutLimitGrid");
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
				logger.Trace("Creating RunInLimit");
				Label RunInLimit = DTACElementStyles.TimetableRunLimitLabel<Label>();
				RunInLimit.HorizontalOptions = LayoutOptions.Start;
				RunInLimit.Text = rowData.RunInLimit.ToString();
				RunInOutLimitGrid.Add(RunInLimit, row: 0);
			}

			if (isRunOutLimitVisible)
			{
				logger.Trace("Creating RunOutLimit");
				Label RunOutLimit = DTACElementStyles.TimetableRunLimitLabel<Label>();
				RunOutLimit.HorizontalOptions = LayoutOptions.End;
				RunOutLimit.Text = rowData.RunOutLimit.ToString();
				RunInOutLimitGrid.Add(RunOutLimit, row: 1);
			}

			parent.Add(RunInOutLimitGrid, 5, rowIndex);
		}
		else
		{
			logger.Debug("RunInLimit and RunOutLimit are not visible value, so skipping...");
		}
		#endregion

		if (!string.IsNullOrEmpty(rowData.Remarks))
		{
			logger.Debug("Creating Remarks");
			HtmlAutoDetectLabel Remarks = DTACElementStyles.TimetableHtmlAutoDetectLabel<HtmlAutoDetectLabel>();
			Remarks.FontAttributes = FontAttributes.None;
			Remarks.HorizontalOptions = LayoutOptions.Start;
			Remarks.FontSize = 16;
			Remarks.Text = rowData.Remarks;
			parent.Add(Remarks, 6, rowIndex);
		}
		else
		{
			logger.Debug("Remarks is null or empty, so skipping...");
		}

		logger.Trace("Creating MarkerBox");
		MarkerBox = new()
		{
			IsVisible = false,
			IsEnabled = true,
			FontFamily = "Hiragino Sans",
			FontSize = 18,
			FontAutoScalingEnabled = false,
			FontAttributes = FontAttributes.Bold,
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

		parent.Add(MarkerBox, 7, rowIndex);

		logger.Trace("Created");
	}
}

