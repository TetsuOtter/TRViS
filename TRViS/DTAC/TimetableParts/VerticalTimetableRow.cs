using TRViS.Controls;
using TRViS.ValueConverters.DTAC;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using TRViS.DTAC.ViewModels;
using TRViS.ViewModels;

namespace TRViS.DTAC.TimetableParts;

public class VerticalTimetableRow : IDisposable
{
	const double BG_ALPHA = 0.3;

	private const int DRIVE_TIME_COLUMN = 0;
	private const int STATION_NAME_COLUMN = 1;
	private const int ARRIVAL_TIME_COLUMN = 2;
	private const int DEPARTURE_TIME_COLUMN = 3;
	private const int TRACK_NAME_COLUMN = 4;
	private const int RUN_IN_OUT_LIMIT_COLUMN = 5;
	private const int REMARKS_COLUMN = 6;
	private const int MARKER_COLUMN = 7;

	private Grid ParentGrid { get; }
	private VerticalTimetableColumnVisibilityState VisibilityState { get; }
	public VerticalTimetableRowModel Model { get; }

	[MemberNotNullWhen(false, nameof(BackgroundBoxView))]
	private bool _disposed { get; set; } = false;

	private BoxView? BackgroundBoxView;

	// DriveTime components
	private Grid? DriveTimeGrid;
	private Label? DriveTimeMMLabel;
	private Label? DriveTimeSSLabel;

	// StationName component
	private HtmlAutoDetectLabel? StationNameLabel;

	// ArrivalTime components
	private TimeCell? ArrivalTimeCell;
	private (Label Open, Label Close)? Brackets;
	private (Label Open, Label Close)? OpOnlyStopBrackets;

	// DepartureTime components
	private TimeCell? DepartureTimeCell;
	private Grid? LastStopLineGrid;

	// TrackName component
	private HtmlAutoDetectLabel? TrackNameLabel;

	// RunInOutLimit components
	private Grid? RunInOutLimitGrid;
	private Label? RunInLimitLabel;
	private Label? RunOutLimitLabel;

	// Remarks component
	private HtmlAutoDetectLabel? RemarksLabel;

	// Marker component
	private Button? MarkerBox;

	// InfoRow component
	private HtmlAutoDetectLabel? InfoRowLabel;

	public event EventHandler? MarkerBoxClicked;
	public event EventHandler? RowTapped;

	public VerticalTimetableRow(Grid parentGrid, VerticalTimetableRowModel model, VerticalTimetableColumnVisibilityState visibilityState, DTACMarkerViewModel markerViewModel, bool isLastRow)
	{
		ParentGrid = parentGrid;
		VisibilityState = visibilityState;
		Model = model;

		// BackgroundBoxViewの作成
		BackgroundBoxView = new BoxView
		{
			Color = Colors.Transparent,
			Opacity = BG_ALPHA,
			ZIndex = DTACElementStyles.TimetableRowMarkerBackgroundZIndex,
		};
		Grid.SetRow(BackgroundBoxView, Model.RowIndex);
		Grid.SetColumnSpan(BackgroundBoxView, 8);
		ParentGrid.Add(BackgroundBoxView);

		// TapGestureRecognizerの追加
		TapGestureRecognizer tapGesture = new();
		tapGesture.Tapped += (s, e) => RowTapped?.Invoke(this, e);
		BackgroundBoxView.GestureRecognizers.Add(tapGesture);

		// イベントハンドラーの登録
		Model.PropertyChanged += OnModelPropertyChanged;
		VisibilityState.PropertyChanged += OnVisibilityStatePropertyChanged;

		// 初期化
		UpdateAllComponents();
	}

	private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(VerticalTimetableRowModel.RowIndex):
				UpdateRowIndex();
				break;
			case nameof(VerticalTimetableRowModel.IsInfoRow):
			case nameof(VerticalTimetableRowModel.InfoText):
				UpdateAllComponents();
				break;
			case nameof(VerticalTimetableRowModel.DriveTimeMM):
			case nameof(VerticalTimetableRowModel.DriveTimeSS):
				UpdateDriveTime();
				break;
			case nameof(VerticalTimetableRowModel.StationName):
				UpdateStationName();
				break;
			case nameof(VerticalTimetableRowModel.ArrivalTime):
			case nameof(VerticalTimetableRowModel.HasBracket):
				UpdateArrivalTime();
				break;
			case nameof(VerticalTimetableRowModel.IsOperationOnlyStop):
				UpdateOperationOnlyStop();
				break;
			case nameof(VerticalTimetableRowModel.IsPass):
				UpdateArrivalTime();
				UpdateDepartureTime();
				break;
			case nameof(VerticalTimetableRowModel.DepartureTime):
			case nameof(VerticalTimetableRowModel.IsLastStop):
				UpdateDepartureTime();
				break;
			case nameof(VerticalTimetableRowModel.TrackName):
				UpdateTrackName();
				break;
			case nameof(VerticalTimetableRowModel.RunInLimit):
			case nameof(VerticalTimetableRowModel.RunOutLimit):
				UpdateRunInOutLimit();
				break;
			case nameof(VerticalTimetableRowModel.Remarks):
				UpdateRemarks();
				break;
			case nameof(VerticalTimetableRowModel.MarkerColor):
			case nameof(VerticalTimetableRowModel.MarkerText):
			case nameof(VerticalTimetableRowModel.IsMarkingMode):
				UpdateMarker();
				break;
			case nameof(VerticalTimetableRowModel.IsLocationMarkerOnThisRow):
				UpdateDriveTimeTextColor();
				break;
		}
	}

	private void OnVisibilityStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(VerticalTimetableColumnVisibilityState.RunTime):
				UpdateDriveTime();
				break;
			case nameof(VerticalTimetableColumnVisibilityState.StationName):
				UpdateStationName();
				break;
			case nameof(VerticalTimetableColumnVisibilityState.ArrivalTime):
				UpdateArrivalTime();
				UpdateOperationOnlyStop();
				break;
			case nameof(VerticalTimetableColumnVisibilityState.DepartureTime):
				UpdateDepartureTime();
				UpdateOperationOnlyStop();
				break;
			case nameof(VerticalTimetableColumnVisibilityState.TrackName):
				UpdateTrackName();
				break;
			case nameof(VerticalTimetableColumnVisibilityState.RunInOutLimit):
				UpdateRunInOutLimit();
				break;
			case nameof(VerticalTimetableColumnVisibilityState.Remarks):
				UpdateRemarks();
				break;
			case nameof(VerticalTimetableColumnVisibilityState.Marker):
				UpdateMarker();
				break;
		}
	}

	// Util系
	private void SetRowIfAttached(View? view)
	{
		if (view?.Parent is null)
			return;
		Grid.SetRow(view, Model.RowIndex);
	}

	private void RemoveComponent<T>(ref T? component) where T : View
	{
		if (component is null)
			return;
		ParentGrid.Remove(component);
		component = null;
	}

	[MemberNotNull(nameof(BackgroundBoxView))]
	private T EnsureComponent<T>([NotNull] ref T? component, Func<T> createFunc, int column) where T : View
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(VerticalTimetableRow));

		component ??= createFunc();
		if (component.Parent is null)
		{
			ParentGrid.Add(component, column, Model.RowIndex);
		}
		return component;
	}

	private T EnsureComponent<T>([NotNull] ref T? component, Func<T> createFunc, Grid parentGrid, int column, int row) where T : View
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(VerticalTimetableRow));

		component ??= createFunc();
		if (component.Parent is null)
		{
			parentGrid.Add(component, column, row);
		}
		return component;
	}

	private void OnMarkerBoxClicked(object? sender, EventArgs e)
	{
		MarkerBoxClicked?.Invoke(this, e);
	}

	/// <summary>
	/// マーカーテキストを全角2文字（半角4文字相当）までに制限します
	/// 全角文字は2、半角文字は1としてカウントします
	/// </summary>
	private static string? LimitMarkerText(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return text;

		var elementEnumerator = StringInfo.GetTextElementEnumerator(text);
		int width = 0;
		int charIndex = 0;

		while (elementEnumerator.MoveNext())
		{
			string element = elementEnumerator.GetTextElement();

			// 文字の幅を判定（全角は2、半角は1）
			int elementWidth = IsFullWidth(element) ? 2 : 1;

			if (width + elementWidth > 4)
			{
				// 4文字相当を超えるので切断
				return text.Substring(0, charIndex);
			}

			width += elementWidth;
			charIndex += element.Length;
		}

		return text;
	}

	/// <summary>
	/// 文字が全角かどうかを判定します
	/// </summary>
	private static bool IsFullWidth(string text)
	{
		if (string.IsNullOrEmpty(text))
			return false;

		// 最初の文字のUnicode カテゴリを確認
		char ch = text[0];

		// 一般的な全角文字の判定
		// ひらがな、カタカナ、漢字、全角記号など
		return char.GetUnicodeCategory(ch) switch
		{
			UnicodeCategory.OtherLetter => true,      // CJK文字など
			UnicodeCategory.OtherSymbol => true,       // 全角記号
			UnicodeCategory.OtherPunctuation => true,  // 全角句読点
			_ => false
		};
	}

	// Create系
	private void EnsureDriveTimeComponents()
	{
		EnsureComponent(ref DriveTimeGrid, () => new()
		{
			Margin = new Thickness(2, 0),
			VerticalOptions = LayoutOptions.End,
			ColumnDefinitions =
			{
				new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)),
				new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
			},
			InputTransparent = true,
			ZIndex = DTACElementStyles.TimetableRowRunTimeTextZIndex + 1,
		}, DRIVE_TIME_COLUMN);

		if (!string.IsNullOrEmpty(Model.DriveTimeMM))
		{
			EnsureComponent(ref DriveTimeMMLabel, DTACElementStyles.TimetableDriveTimeMMLabel<Label>, DriveTimeGrid, 0, 0);
		}

		if (!string.IsNullOrEmpty(Model.DriveTimeSS))
		{
			EnsureComponent(ref DriveTimeSSLabel, DTACElementStyles.TimetableDriveTimeSSLabel<Label>, DriveTimeGrid, 1, 0);
		}
	}

	private static HtmlAutoDetectLabel CreateStationNameComponent()
	{
		var label = DTACElementStyles.TimetableHtmlAutoDetectLabel<HtmlAutoDetectLabel>();
		label.Margin = new Thickness(0);
		return label;
	}

	private static (Label Open, Label Close) CreateBracketComponents()
	{
		var open = DTACElementStyles.TimetableLabel<Label>();
		open.HorizontalOptions = LayoutOptions.Start;
		open.Text = "(";

		var close = DTACElementStyles.TimetableLabel<Label>();
		close.HorizontalOptions = LayoutOptions.End;
		close.Text = ")";

		return (open, close);
	}

	private static (Label Open, Label Close) CreateOpOnlyStopComponents()
	{
		var open = DTACElementStyles.TimetableLabel<Label>();
		open.HorizontalOptions = LayoutOptions.Start;
		open.Text = "[";

		var close = DTACElementStyles.TimetableLabel<Label>();
		close.HorizontalOptions = LayoutOptions.End;
		close.Text = "]";

		return (open, close);
	}

	private static HtmlAutoDetectLabel CreateTrackNameComponent()
	{
		var label = DTACElementStyles.TimetableHtmlAutoDetectLabel<HtmlAutoDetectLabel>();
		label.Margin = new Thickness(0);
		label.Padding = new Thickness(0);
		label.HorizontalOptions = LayoutOptions.Center;
		label.VerticalOptions = LayoutOptions.Center;
		label.HorizontalTextAlignment = TextAlignment.Center;
		label.TextColor = Colors.Red;
		label.CurrentAppThemeColorBindingExtension = null;
		return label;
	}

	private void EnsureRunInOutLimitComponents()
	{
		EnsureComponent(ref RunInOutLimitGrid, () => new()
		{
			Margin = new Thickness(10, 4),
			Padding = new Thickness(0),
			RowDefinitions =
			{
				new RowDefinition(GridLength.Star),
				new RowDefinition(GridLength.Star),
			},
			InputTransparent = true,
		}, RUN_IN_OUT_LIMIT_COLUMN);

		if (!string.IsNullOrEmpty(Model.RunInLimit))
		{
			EnsureComponent(ref RunInLimitLabel, () =>
			{
				var label = DTACElementStyles.TimetableRunLimitLabel<Label>();
				label.HorizontalOptions = LayoutOptions.Start;
				return label;
			}, RunInOutLimitGrid, 0, 0);
		}

		if (!string.IsNullOrEmpty(Model.RunOutLimit))
		{
			EnsureComponent(ref RunOutLimitLabel, () =>
			{
				var label = DTACElementStyles.TimetableRunLimitLabel<Label>();
				label.HorizontalOptions = LayoutOptions.End;
				return label;
			}, RunInOutLimitGrid, 0, 1);
		}
	}

	private static HtmlAutoDetectLabel CreateRemarksComponent()
	{
		var label = DTACElementStyles.TimetableHtmlAutoDetectLabel<HtmlAutoDetectLabel>();
		label.FontAttributes = FontAttributes.None;
		label.HorizontalOptions = LayoutOptions.Start;
		label.FontSize = 16;
		return label;
	}

	// Update系
	private void UpdateInfoRow()
	{
		if (!Model.IsInfoRow || string.IsNullOrEmpty(Model.InfoText))
		{
			RemoveComponent(ref InfoRowLabel);
			return;
		}

		EnsureComponent(ref InfoRowLabel, DTACElementStyles.LargeHtmlAutoDetectLabelStyle<HtmlAutoDetectLabel>, STATION_NAME_COLUMN);
		InfoRowLabel.Text = Model.InfoText;
		InfoRowLabel.HorizontalOptions = LayoutOptions.Start;
		Grid.SetColumnSpan(InfoRowLabel, 6);
	}
	private void UpdateAllComponents()
	{
		if (Model.IsInfoRow)
		{
			UpdateInfoRow();
		}
		else
		{
			UpdateDriveTime();
			UpdateStationName();
			UpdateArrivalTime();
			UpdateOperationOnlyStop();
			UpdateDepartureTime();
			UpdateTrackName();
			UpdateRunInOutLimit();
			UpdateRemarks();
			UpdateMarker();
		}
	}

	private void UpdateRowIndex()
	{
		Grid.SetRow(BackgroundBoxView, Model.RowIndex);

		SetRowIfAttached(DriveTimeGrid);
		SetRowIfAttached(StationNameLabel);
		SetRowIfAttached(ArrivalTimeCell);
		if (Brackets is var (open, close))
		{
			SetRowIfAttached(open);
			SetRowIfAttached(close);
		}
		if (OpOnlyStopBrackets is var (opOpen, opClose))
		{
			SetRowIfAttached(opOpen);
			SetRowIfAttached(opClose);
		}
		SetRowIfAttached(DepartureTimeCell);
		SetRowIfAttached(LastStopLineGrid);
		SetRowIfAttached(TrackNameLabel);
		SetRowIfAttached(RunInOutLimitGrid);
		SetRowIfAttached(RemarksLabel);
		SetRowIfAttached(MarkerBox);
		SetRowIfAttached(InfoRowLabel);
	}

	private void UpdateDriveTime()
	{
		if (!VisibilityState.RunTime || (string.IsNullOrEmpty(Model.DriveTimeMM) && string.IsNullOrEmpty(Model.DriveTimeSS)))
		{
			// 削除
			RemoveComponent(ref DriveTimeGrid);
			DriveTimeMMLabel = null;
			DriveTimeSSLabel = null;
			return;
		}

		EnsureDriveTimeComponents();
		UpdateDriveTimeValues();
	}

	private void UpdateDriveTimeValues()
	{
		if (!string.IsNullOrEmpty(Model.DriveTimeMM) && DriveTimeMMLabel is not null)
		{
			DriveTimeMMLabel.Text = Model.DriveTimeMM;
		}
		if (!string.IsNullOrEmpty(Model.DriveTimeSS) && DriveTimeSSLabel is not null)
		{
			string text = Model.DriveTimeSS ?? "";
			if (text.Length == 1)
			{
				text = "  " + text;
			}
			DriveTimeSSLabel.Text = text;
		}
		UpdateDriveTimeTextColor();
	}

	private void UpdateStationName()
	{
		if (!VisibilityState.StationName || string.IsNullOrEmpty(Model.StationName))
		{
			RemoveComponent(ref StationNameLabel);
			return;
		}

		var label = EnsureComponent(ref StationNameLabel, CreateStationNameComponent, STATION_NAME_COLUMN);
		label.Text = StationNameConverter.Convert(Model.StationName);
	}

	private void UpdateArrivalTime()
	{
		// ArrivalTimeCell
		if (!VisibilityState.ArrivalTime || Model.ArrivalTime is null)
		{
			RemoveComponent(ref ArrivalTimeCell);
		}
		else
		{
			EnsureComponent(ref ArrivalTimeCell, DTACElementStyles.TimeCell, ARRIVAL_TIME_COLUMN);
			ArrivalTimeCell.TimeData = Model.ArrivalTime;
			ArrivalTimeCell.IsPass = Model.IsPass;
		}

		// Bracket
		if (!Model.HasBracket)
		{
			if (Brackets is var (open, close))
			{
				RemoveComponent(ref open);
				RemoveComponent(ref close);
			}
			Brackets = null;
		}
		else if (Brackets is null)
		{
			Brackets = CreateBracketComponents();
			var (open, close) = Brackets.Value;
			ParentGrid.Add(open, ARRIVAL_TIME_COLUMN, Model.RowIndex);
			ParentGrid.Add(close, ARRIVAL_TIME_COLUMN, Model.RowIndex);
		}
	}

	private void UpdateOperationOnlyStop()
	{
		// OpOnlyStop
		if (!Model.IsOperationOnlyStop || (!VisibilityState.ArrivalTime && !VisibilityState.DepartureTime))
		{
			if (OpOnlyStopBrackets is var (opOpen, opClose))
			{
				RemoveComponent(ref opOpen);
				RemoveComponent(ref opClose);
			}
			OpOnlyStopBrackets = null;
		}
		else if (OpOnlyStopBrackets is null)
		{
			OpOnlyStopBrackets = CreateOpOnlyStopComponents();
			var (opOpen, opClose) = OpOnlyStopBrackets.Value;
			ParentGrid.Add(opOpen, ARRIVAL_TIME_COLUMN, Model.RowIndex);
			ParentGrid.Add(opClose, DEPARTURE_TIME_COLUMN, Model.RowIndex);
		}
	}

	private void UpdateDepartureTime()
	{
		// DepartureTimeCell
		if (!VisibilityState.DepartureTime || Model.DepartureTime is null)
		{
			RemoveComponent(ref DepartureTimeCell);
		}
		else
		{
			EnsureComponent(ref DepartureTimeCell, DTACElementStyles.TimeCell, DEPARTURE_TIME_COLUMN);
			DepartureTimeCell.TimeData = Model.DepartureTime;
			DepartureTimeCell.IsPass = Model.IsPass;
		}

		// LastStopLine
		if (!Model.IsLastStop)
		{
			RemoveComponent(ref LastStopLineGrid);
		}
		else
		{
			EnsureComponent(ref LastStopLineGrid, DTACElementStyles.LastStopLineGrid, DEPARTURE_TIME_COLUMN);
		}
	}

	private void UpdateTrackName()
	{
		if (!VisibilityState.TrackName || string.IsNullOrEmpty(Model.TrackName))
		{
			RemoveComponent(ref TrackNameLabel);
			return;
		}

		var label = EnsureComponent(ref TrackNameLabel, CreateTrackNameComponent, TRACK_NAME_COLUMN);
		label.Text = Model.TrackName;
		label.FontSize = DTACElementStyles.GetTimetableTrackLabelFontSize(Model.TrackName, label.FontSize);
	}

	private void UpdateRunInOutLimit()
	{
		if (!VisibilityState.RunInOutLimit || (string.IsNullOrEmpty(Model.RunInLimit) && string.IsNullOrEmpty(Model.RunOutLimit)))
		{
			RemoveComponent(ref RunInOutLimitGrid);
			RunInLimitLabel = null;
			RunOutLimitLabel = null;
			return;
		}

		EnsureRunInOutLimitComponents();
		UpdateRunInOutLimitValues();
	}

	private void UpdateRunInOutLimitValues()
	{
		RunInLimitLabel?.Text = Model.RunInLimit ?? "";
		RunOutLimitLabel?.Text = Model.RunOutLimit ?? "";
	}

	private void UpdateRemarks()
	{
		if (!VisibilityState.Remarks || string.IsNullOrEmpty(Model.Remarks))
		{
			RemoveComponent(ref RemarksLabel);
			return;
		}

		EnsureComponent(ref RemarksLabel, CreateRemarksComponent, REMARKS_COLUMN);
		RemarksLabel.Text = Model.Remarks;
	}

	private void UpdateMarker()
	{
		if (Model.IsInfoRow || !VisibilityState.Marker || (!Model.IsMarkingMode && Model.MarkerColor is null))
		{
			RemoveComponent(ref MarkerBox);
			BackgroundBoxView!.Color = Colors.Transparent;
			return;
		}

		EnsureComponent(ref MarkerBox, () =>
		{
			var button = new Button
			{
				FontFamily = "Hiragino Sans",
				FontSize = 18,
				FontAutoScalingEnabled = false,
				FontAttributes = FontAttributes.Bold,
				BorderColor = Colors.Transparent,
				CornerRadius = 4,
				Padding = new Thickness(0),
				Margin = new Thickness(8),
				HorizontalOptions = LayoutOptions.Start,
				VerticalOptions = LayoutOptions.Center,
				HeightRequest = 40,
				WidthRequest = 40,
				Shadow = DTACElementStyles.DefaultShadow,
				Opacity = 0.9,
				ZIndex = DTACElementStyles.TimetableRowMarkerBoxZIndex,
			};
			button.Shadow.Offset = new Point(2, 2);
			button.Shadow.Radius = 2;
			button.Clicked += OnMarkerBoxClicked;
			return button;
		}, MARKER_COLUMN);
		MarkerBox.IsEnabled = Model.IsMarkingMode;
		if (Model.MarkerColor is null)
		{
			DTACElementStyles.MarkerMarkButtonBGColorBrush.Apply(MarkerBox, VisualElement.BackgroundProperty);
			BackgroundBoxView.Color = Colors.Transparent;
		}
		else
		{
			MarkerBox.Background = new SolidColorBrush(Model.MarkerColor);
			MarkerBox.TextColor = Utils.GetTextColorFromBGColor(Model.MarkerColor);
			BackgroundBoxView.Color = Model.MarkerColor;
		}
		MarkerBox.Text = LimitMarkerText(Model.MarkerText);
	}

	private void DisposeComponents()
	{
		RemoveComponent(ref BackgroundBoxView);
		RemoveComponent(ref DriveTimeGrid);
		DriveTimeMMLabel = null;
		DriveTimeSSLabel = null;
		RemoveComponent(ref StationNameLabel);
		RemoveComponent(ref ArrivalTimeCell);
		if (Brackets is var (open, close))
		{
			RemoveComponent(ref open);
			RemoveComponent(ref close);
		}
		Brackets = null;
		if (OpOnlyStopBrackets is var (opOpen, opClose))
		{
			RemoveComponent(ref opOpen);
			RemoveComponent(ref opClose);
		}
		OpOnlyStopBrackets = null;
		RemoveComponent(ref DepartureTimeCell);
		RemoveComponent(ref LastStopLineGrid);
		RemoveComponent(ref TrackNameLabel);
		RemoveComponent(ref RunInOutLimitGrid);
		RunInLimitLabel = null;
		RunOutLimitLabel = null;
		RemoveComponent(ref RemarksLabel);
		RemoveComponent(ref MarkerBox);
		RemoveComponent(ref InfoRowLabel);
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		DisposeComponents();
	}

	private void UpdateDriveTimeTextColor()
	{
		if (!MainThread.IsMainThread)
		{
			MainThread.BeginInvokeOnMainThread(() => UpdateDriveTimeTextColor());
			return;
		}

		AppThemeColorBindingExtension color = Model.IsLocationMarkerOnThisRow ? DTACElementStyles.TimetableTextInvColor : DTACElementStyles.TimetableTextColor;
		color.Apply(DriveTimeMMLabel, Label.TextColorProperty);
		color.Apply(DriveTimeSSLabel, Label.TextColorProperty);
	}
}
