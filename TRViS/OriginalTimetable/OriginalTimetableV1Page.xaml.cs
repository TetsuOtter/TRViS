using TRViS.Services;

namespace TRViS.OriginalTimetable;

// V1 Modern Classic — 独自時刻表ページ骨格
public partial class OriginalTimetableV1Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV1Page);

	public OriginalTimetableV1Page()
	{
		InitializeComponent();
		BindingContext = InstanceManager.OriginalTimetableViewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.Portrait);
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
	}
}
