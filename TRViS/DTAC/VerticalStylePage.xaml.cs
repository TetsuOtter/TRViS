using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class VerticalStylePage : ContentPage
{
	static public ColumnDefinitionCollection TimetableColumnWidthCollection => new(
		new(new(60)),
		new(new(136)),
		new(new(132)),
		new(new(132)),
		new(new(60)),
		new(new(60)),
		new(new(1, GridUnitType.Star)),
		new(new(64))
		);

	const double INPAGE_TAB_ROW_HEIGHT = 64;
	const double DATE_AND_START_BUTTON_ROW_HEIGHT = 64;
	const double TRAIN_INFO_HEADER_ROW_HEIGHT = 54;
	const double TRAIN_INFO_ROW_HEIGHT = 60;
	const double CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT = 60;
	const double TIMETABLE_HEADER_ROW_HEIGHT = 54;
	const double TRAIN_REMARKS_ROW_HEIGHT = 64;
	static public RowDefinitionCollection PageRowDefinitionCollection => new(
		new(new(INPAGE_TAB_ROW_HEIGHT)),
		new(new(DATE_AND_START_BUTTON_ROW_HEIGHT)),
		new(new(TRAIN_INFO_HEADER_ROW_HEIGHT)),
		new(new(TRAIN_INFO_ROW_HEIGHT)),
		new(new(CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT)),
		new(new(TIMETABLE_HEADER_ROW_HEIGHT)),
		new(new(1, GridUnitType.Star)),
		new(new(TRAIN_REMARKS_ROW_HEIGHT))
		);

	public static double TimetableViewActivityIndicatorFrameMaxOpacity { get; } = 0.6;

	public VerticalStylePage(AppViewModel viewModel)
	{
		InitializeComponent();

		BindingContext = viewModel;

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
			Content = new ScrollView()
			{
				Content = this.Content
			};

		Task.Run(() =>
		{
			VerticalTimetableView view = new();

			view.IsBusyChanged += (s, _) =>
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
			};

			view.IgnoreSafeArea = false;
			view.VerticalOptions = LayoutOptions.Start;
			view.SetBinding(VerticalTimetableView.SelectedTrainDataProperty, new Binding()
			{
				Source = viewModel,
				Path = nameof(AppViewModel.SelectedTrainData)
			});

			MainThread.BeginInvokeOnMainThread(() => TimetableAreaScrollView.Content = view);
		});
	}
}
