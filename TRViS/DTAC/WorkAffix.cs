using TRViS.Controls;

namespace TRViS.DTAC;

public class WorkAffix : ContentView
{
	public WorkAffix()
	{
		BackgroundColor = Colors.White;

		LogView logView = new()
		{
			PriorityFilter
				= LogView.Priority.Info
				| LogView.Priority.Warn
				| LogView.Priority.Error
		};
		Content = logView;
		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);
	}
}
