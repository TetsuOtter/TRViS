using TRViS.Services;

namespace TRViS.OriginalTimetable;

// V2 Card Stack — 独自時刻表ページ骨格
public partial class OriginalTimetableV2Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV2Page);

	public OriginalTimetableV2Page()
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
