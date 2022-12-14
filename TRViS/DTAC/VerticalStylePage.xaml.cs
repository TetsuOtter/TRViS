using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<TrainData>("SelectedTrainData")]
[DependencyProperty<string>("AffectDate")]
public partial class VerticalStylePage : ContentView
{
	public const double RUN_TIME_COLUMN_WIDTH = 60;
	static public ColumnDefinitionCollection TimetableColumnWidthCollection => new(
		new(new(RUN_TIME_COLUMN_WIDTH)),
		new(new(140)),
		new(new(140)),
		new(new(140)),
		new(new(60)),
		new(new(60)),
		new(new(1, GridUnitType.Star)),
		new(new(64))
		);

	const double DATE_AND_START_BUTTON_ROW_HEIGHT = 60;
	const double TRAIN_INFO_HEADER_ROW_HEIGHT = 54;
	const double TRAIN_INFO_ROW_HEIGHT = 60;
	const double CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT = 60;
	const double TIMETABLE_HEADER_ROW_HEIGHT = 60;
	static public RowDefinitionCollection PageRowDefinitionCollection => new(
		new(new(DATE_AND_START_BUTTON_ROW_HEIGHT)),
		new(new(TRAIN_INFO_HEADER_ROW_HEIGHT)),
		new(new(TRAIN_INFO_ROW_HEIGHT)),
		new(new(CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT)),
		new(new(TIMETABLE_HEADER_ROW_HEIGHT)),
		new(new(1, GridUnitType.Star))
		);

	const double CONTENT_OTHER_THAN_TIMETABLE_HEIGHT
		= DATE_AND_START_BUTTON_ROW_HEIGHT
		+ TRAIN_INFO_HEADER_ROW_HEIGHT
		+ TRAIN_INFO_ROW_HEIGHT
		+ CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT
		+ TIMETABLE_HEADER_ROW_HEIGHT;

	public static double TimetableViewActivityIndicatorFrameMaxOpacity { get; } = 0.6;

	VerticalTimetableView TimetableView { get; } = new();

	public VerticalStylePage()
	{
		InitializeComponent();

		this.TimetableHeader.MarkerSettings = TimetableView.MarkerViewModel;

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
			Content = new ScrollView()
			{
				Content = this.Content,
				BackgroundColor = Colors.White
			};

		TimetableView.IsBusyChanged += (s, _) =>
		{
			if (s is not VerticalTimetableView v)
				return;

			if (v.IsBusy)
			{
				TimetableViewActivityIndicatorFrame.IsVisible = true;
				TimetableViewActivityIndicatorFrame.FadeTo(TimetableViewActivityIndicatorFrameMaxOpacity);
			}
			else
				TimetableViewActivityIndicatorFrame.FadeTo(0).ContinueWith((_) => TimetableViewActivityIndicatorFrame.IsVisible = false);

			// iPhone???????????????????????????????????????ScrollView???DesiredSize?????????????????????????????????????????????????????????
			if (Content is ScrollView sv)
				sv.Content.HeightRequest = Math.Max(this.Height,
					CONTENT_OTHER_THAN_TIMETABLE_HEIGHT + Math.Max(0, TimetableView.HeightRequest));
		};

		TimetableView.IgnoreSafeArea = false;
		TimetableView.VerticalOptions = LayoutOptions.Start;

		TimetableView.SetBinding(VerticalTimetableView.IsRunStartedProperty, new Binding()
		{
			Source = this.StartEndRunButton,
			Path = nameof(StartEndRunButton.IsChecked)
		});

		TimetableView.ScrollRequested += VerticalTimetableView_ScrollRequested;

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			Grid.SetRow(TimetableView, Grid.GetRow(TimetableAreaScrollView));
			TimetableAreaScrollView.IsVisible = false;
			MainGrid.Add(TimetableView);
		}
		else
			TimetableAreaScrollView.Content = TimetableView;
	}

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		BindingContext = newValue;
		TimetableView.SelectedTrainData = newValue;

		// TODO: https://github.com/TetsuOtter/TRViS/issues/10
		// ?????????????????????????????????????????????????????????????????????????????????????????????
		AffectDate = (newValue?.AffectDate ?? DateOnly.FromDateTime(DateTime.Now)).ToString("yyyy???M???d???");
	}

	private async void VerticalTimetableView_ScrollRequested(object? sender, VerticalTimetableView.ScrollRequestedEventArgs e)
	{
		if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone && DeviceInfo.Current.Idiom != DeviceIdiom.Unknown)
			await TimetableAreaScrollView.ScrollToAsync(TimetableAreaScrollView.ScrollX, e.PositionY, true);
	}
}
