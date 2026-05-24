using TRViS.Services;

namespace TRViS.OriginalTimetable;

// V6 Bold Editorial — 独自時刻表ページ骨格
public partial class OriginalTimetableV6Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV6Page);

	public OriginalTimetableV6Page()
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
