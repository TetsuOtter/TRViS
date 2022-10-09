using TRViS.IO.Models;

namespace TRViS.DTAC;

public partial class VerticalTimetableRow : Grid
{
	public VerticalTimetableRow()
	{
		InitializeComponent();
	}

	public VerticalTimetableRow(TimetableRow rowData)
	{
		InitializeComponent();

		BindingContext = rowData;
	}
}
