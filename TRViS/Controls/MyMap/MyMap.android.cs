#if ANDROID
namespace TRViS.Controls;

public class MyMap : MyMapBase
{
	public MyMap()
	{
		Content = new Label()
		{
			Text = "MyMap is not implemented on Android",
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
		};
	}

	public override void SetIsLocationServiceEnabled(bool isEnabled) {}
	public override void SetCurrentLocation(double latitude, double longitude, double accuracy_m) {}
	public override void SetTimetableRowList(TimetableRow[]? Rows) {}
}

#endif
