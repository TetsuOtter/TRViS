using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<TrainData>("SelectedTrainData")]
[DependencyProperty<string>("AffectDate")]
public partial class VerticalStylePage : ContentView
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public const double DATE_AND_START_BUTTON_ROW_HEIGHT = 60;
	const double TRAIN_INFO_HEADER_ROW_HEIGHT = 54;
	const double TRAIN_INFO_ROW_HEIGHT = 54;
	const double TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT = DTACElementStyles.BeforeDeparture_AfterArrive_Height * 2;
	const double CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT = 54;
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
	DTACViewHostViewModel DTACViewHostViewModel { get; }
	TrainData? CurrentShowingTrainData { get; set; }

	public VerticalStylePage()
	{
		logger.Trace("Creating...");

		DTACViewHostViewModel = InstanceManager.DTACViewHostViewModel;
		DTACViewHostViewModel.PropertyChanged += (_, e) =>
		{
			switch (e.PropertyName)
			{
				case nameof(DTACViewHostViewModel.IsViewHostVisible):
				case nameof(DTACViewHostViewModel.IsVerticalViewMode):
					OnSelectedTrainDataChanged(SelectedTrainData);
					break;
			}
		};

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

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			logger.Info("Device is Phone or Unknown -> make it to fill-scrollable");
			this.Content.VerticalOptions = LayoutOptions.Start;
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

			logger.Info("IsBusyChanged: {0}", v.IsBusy);

			if (v.IsBusy)
			{
				TimetableViewActivityIndicatorFrame.IsVisible = true;
				TimetableViewActivityIndicatorFrame.FadeTo(TimetableViewActivityIndicatorFrameMaxOpacity);
			}
			else
				TimetableViewActivityIndicatorFrame.FadeTo(0).ContinueWith((_) => {
					logger.Debug("TimetableViewActivityIndicatorFrame.FadeTo(0) completed");
					TimetableViewActivityIndicatorFrame.IsVisible = false;
				});

			// iPhoneにて、画面を回転させないとScrollViewのDesiredSizeが正常に更新されないバグに対応するため
			if (Content is ScrollView sv)
			{
				double heightRequest = CONTENT_OTHER_THAN_TIMETABLE_HEIGHT + Math.Max(0, TimetableView.HeightRequest);
				logger.Debug("set full-scrollable-ScrollView.HeightRequest -> Max(this.HeightRequest: {0}, heightRequest: {1})", this.HeightRequest, heightRequest);
				sv.Content.HeightRequest = Math.Max(this.Height, heightRequest);
			}
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
			logger.Info("Device is Phone or Unknown -> set ScrollView to main grid");
			Grid.SetRow(TimetableView, Grid.GetRow(TimetableAreaScrollView));
			TimetableAreaScrollView.IsVisible = false;
			MainGrid.Add(TimetableView);
		}
		else
		{
			logger.Info("Device is not Phone nor Unknown -> set TimetableView to TimetableAreaScrollView");
			TimetableAreaScrollView.Content = TimetableView;
			TimetableView.SetBinding(VerticalTimetableView.ScrollViewHeightProperty, new Binding()
			{
				Source = TimetableAreaScrollView,
				Path = nameof(TimetableAreaScrollView.Height)
			});
		}

		PageHeaderArea.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;
		TimetableView.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;

		logger.Trace("Created");
	}

	private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);
		PageHeaderArea.IsLocationServiceEnabled = e.NewValue;
		TimetableView.IsLocationServiceEnabled = e.NewValue;
	}

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		if (CurrentShowingTrainData == newValue)
		{
			logger.Debug("CurrentShowingTrainData == newValue -> do nothing");
			return;
		}
		if (!DTACViewHostViewModel.IsViewHostVisible || !DTACViewHostViewModel.IsVerticalViewMode)
		{
			logger.Debug("IsViewHostVisible: {0}, IsVerticalViewMode: {1} -> lazy load",
				DTACViewHostViewModel.IsViewHostVisible,
				DTACViewHostViewModel.IsVerticalViewMode
			);
			return;
		}
		CurrentShowingTrainData = newValue;
		logger.Info("SelectedTrainDataChanged: {0}", newValue);
		BindingContext = newValue;
		TimetableView.SelectedTrainData = newValue;

		TrainInfo_BeforeDepartureArea.TrainInfoText = newValue?.TrainInfo ?? "";
		TrainInfo_BeforeDepartureArea.BeforeDepartureText = newValue?.BeforeDeparture ?? "";
		TrainInfo_BeforeDepartureArea.BeforeDepartureText_OnStationTrackColumn = newValue?.BeforeDepartureOnStationTrackCol ?? "";

		SetDestinationString(newValue?.Destination);

		int dayCount = newValue?.DayCount ?? 0;
		this.IsNextDayLabel.IsVisible = dayCount > 0;
	}

	partial void OnAffectDateChanged(string? newValue)
	 => PageHeaderArea.AffectDateLabelText = newValue ?? "";

	private async void VerticalTimetableView_ScrollRequested(object? sender, VerticalTimetableView.ScrollRequestedEventArgs e)
	{
		if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone && DeviceInfo.Current.Idiom != DeviceIdiom.Unknown)
		{
			logger.Debug("Device is not Phone nor Unknown -> scroll from {0} to {1}",
				TimetableAreaScrollView.ScrollY,
				e.PositionY);
			await TimetableAreaScrollView.ScrollToAsync(TimetableAreaScrollView.ScrollX, e.PositionY, true);
		}
		else
		{
			logger.Debug("Device is Phone or Unknown -> do nothing");
		}
	}

	const string DateAndStartButton_AnimationName = nameof(DateAndStartButton_AnimationName);
	void BeforeRemarks_TrainInfo_OpenCloseChanged(object sender, ValueChangedEventArgs<bool> e)
	{
		bool isToOpen = e.NewValue;
		(double start, double end) = isToOpen
			? (TrainInfo_BeforeDepature_RowDefinition.Height.Value, TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT)
			: (TrainInfo_BeforeDepature_RowDefinition.Height.Value, 0d);
		logger.Info("BeforeRemarks_TrainInfo_OpenCloseChanged: {0} -> {1} / pos {2} -> {3}",
			e.OldValue,
			e.NewValue,
			start,
			end
		);

		if (this.AnimationIsRunning(DateAndStartButton_AnimationName))
		{
			logger.Debug("AbortAnimation({0})", DateAndStartButton_AnimationName);
			this.AbortAnimation(DateAndStartButton_AnimationName);
		}
		new Animation(
			v => {
				if (!TrainInfo_BeforeDepartureArea.IsVisible)
				{
					logger.Debug("TrainInfo_BeforeDepartureArea.IsVisible set to true");
					TrainInfo_BeforeDepartureArea.IsVisible = true;
				}
				TrainInfo_BeforeDepature_RowDefinition.Height = v;
				TrainInfo_BeforeDepartureArea.HeightRequest = v;
				logger.Trace("v: {0}", v);
			},
			start,
			end,
			Easing.SinInOut
		)
			.Commit(
				this,
				DateAndStartButton_AnimationName,
				finished: (_, canceled) => {
					if (!isToOpen && !canceled)
					{
						logger.Debug("Animation Successfully finished to close");
						TrainInfo_BeforeDepartureArea.IsVisible = false;
					}
					else
					{
						logger.Debug("Animation Successfully finished to open or canceled");
					}
				}
			);
		logger.Debug("Animation started");
	}

	string? _DestinationString = null;
	void SetDestinationString(string? value)
	{
		if (_DestinationString == value)
			return;

		_DestinationString = value;
		if (string.IsNullOrEmpty(value))
		{
			DestinationLabel.IsVisible = false;
			DestinationLabel.Text = null;
			return;
		}

		string dstStr = value;
		switch (value.Length)
		{
			case 1:
				dstStr = $"{Utils.SPACE_CHAR}{value}{Utils.SPACE_CHAR}";
				break;
			case 2:
				dstStr = $"{value[0]}{Utils.SPACE_CHAR}{value[1]}";
				break;
		}

		DestinationLabel.Text = $"（{dstStr}行）";
		DestinationLabel.IsVisible = true;
	}
}
