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
