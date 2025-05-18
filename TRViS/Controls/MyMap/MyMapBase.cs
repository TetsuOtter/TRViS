using TRViS.IO.Models;

namespace TRViS.Controls;

public abstract class MyMapBase : ContentView
{
	public abstract void SetIsLocationServiceEnabled(bool isEnabled);
	public abstract void SetCurrentLocation(double latitude, double longitude, double accuracy_m);
	public abstract void SetTimetableRowList(TimetableRow[]? Rows);
}
