using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<TrainData>("SelectedTrainData")]
[DependencyProperty<string>("AffectDate")]
public partial class VerticalStylePage : ContentView
{
	const double DATE_AND_START_BUTTON_ROW_HEIGHT = 60;
	const double TRAIN_INFO_HEADER_ROW_HEIGHT = 54;
	const double TRAIN_INFO_ROW_HEIGHT = 60;
	const double TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT = DTACElementStyles.BeforeDeparture_AfterArrive_Height * 2;
	const double CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT = 60;
	const double TIMETABLE_HEADER_ROW_HEIGHT = 60;

	RowDefinition TrainInfo_BeforeDepature_RowDefinition { get; } = new(0);

	const double CONTENT_OTHER_THAN_TIMETABLE_HEIGHT
		= DATE_AND_START_BUTTON_ROW_HEIGHT
		+ TRAIN_INFO_HEADER_ROW_HEIGHT
		+ TRAIN_INFO_ROW_HEIGHT
		+ TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT
		+ CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT
		+ TIMETABLE_HEADER_ROW_HEIGHT;

	public static double TimetableViewActivityIndicatorFrameMaxOpacity { get; } = 0.6;

	VerticalTimetableView TimetableView { get; } = new();

	public VerticalStylePage()
	{
		InitializeComponent();

		MainGrid.RowDefinitions = new(
			new(DATE_AND_START_BUTTON_ROW_HEIGHT),
			new(new(TRAIN_INFO_HEADER_ROW_HEIGHT)),
			new(new(TRAIN_INFO_ROW_HEIGHT)),
			TrainInfo_BeforeDepature_RowDefinition,
			new(new(CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT)),
			new(new(TIMETABLE_HEADER_ROW_HEIGHT)),
			new(new(1, GridUnitType.Star))
		);

		this.TimetableHeader.MarkerSettings = TimetableView.MarkerViewModel;

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			Content = new ScrollView()
			{
				Content = this.Content,
			};
			DTACElementStyles.DefaultBGColor.Apply(Content, BackgroundColorProperty);
		}

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

			// iPhoneにて、画面を回転させないとScrollViewのDesiredSizeが正常に更新されないバグに対応するため
			if (Content is ScrollView sv)
				sv.Content.HeightRequest = Math.Max(this.Height,
					CONTENT_OTHER_THAN_TIMETABLE_HEIGHT + Math.Max(0, TimetableView.HeightRequest));
		};

		TimetableView.IgnoreSafeArea = false;
		TimetableView.VerticalOptions = LayoutOptions.Start;

		TimetableView.SetBinding(VerticalTimetableView.IsRunStartedProperty, new Binding()
		{
			Source = this.PageHeaderArea,
			Path = nameof(PageHeader.IsRunning)
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

		PageHeaderArea.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;
		TimetableView.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;
	}

	private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		PageHeaderArea.IsLocationServiceEnabled = e.NewValue;
		TimetableView.IsLocationServiceEnabled = e.NewValue;
	}

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		BindingContext = newValue;
		TimetableView.SelectedTrainData = newValue;

		TrainInfo_BeforeDepartureArea.TrainInfoText = newValue?.TrainInfo ?? "";
		TrainInfo_BeforeDepartureArea.BeforeDepartureText = newValue?.BeforeDeparture ?? "";
		TrainInfo_BeforeDepartureArea.BeforeDepartureText_OnStationTrackColumn = newValue?.BeforeDepartureOnStationTrackCol ?? "";

		int dayCount = newValue?.DayCount ?? 0;
		this.IsNextDayLabel.IsVisible = dayCount > 0;
		AffectDate = (
			newValue?.AffectDate
			?? DateOnly.FromDateTime(DateTime.Now).AddDays(-dayCount)
		).ToString("yyyy年M月d日");
	}

	partial void OnAffectDateChanged(string? newValue)
	 => PageHeaderArea.AffectDateLabelText = newValue ?? "";

	private async void VerticalTimetableView_ScrollRequested(object? sender, VerticalTimetableView.ScrollRequestedEventArgs e)
	{
		if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone && DeviceInfo.Current.Idiom != DeviceIdiom.Unknown)
			await TimetableAreaScrollView.ScrollToAsync(TimetableAreaScrollView.ScrollX, e.PositionY, true);
	}

	const string DateAndStartButton_AnimationName = nameof(DateAndStartButton_AnimationName);
	void BeforeRemarks_TrainInfo_OpenCloseChanged(object sender, ValueChangedEventArgs<bool> e)
	{
		(double start, double end) = e.NewValue ? (TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT, 0d) : (0d, TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT);

		new Animation(v => TrainInfo_BeforeDepature_RowDefinition.Height = v, start, end, Easing.SinInOut)
			.Commit(this, DateAndStartButton_AnimationName, length: 250, finished: (v, _) => TrainInfo_BeforeDepature_RowDefinition.Height = v);
	}
}
