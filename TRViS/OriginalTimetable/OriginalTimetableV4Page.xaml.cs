using TRViS.Services;

namespace TRViS.OriginalTimetable;

// V4 Next Big — 独自時刻表ページ骨格
public partial class OriginalTimetableV4Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV4Page);

	public OriginalTimetableV4Page()
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
