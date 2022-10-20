using DependencyPropertyGenerator;
using TRViS.IO.Models;

namespace TRViS.DTAC;

[ContentProperty(nameof(Content))]
[DependencyProperty<View>("Content")]
[DependencyProperty<IHasRemarksProperty>("RemarksData")]
public partial class WithRemarksView : Grid
{
	Remarks RemarksView { get; } = new();

	public WithRemarksView()
	{
		RowDefinitions.Add(new(new(1, GridUnitType.Star)));
		RowDefinitions.Add(new(new(Remarks.HEADER_HEIGHT, GridUnitType.Absolute)));

		IgnoreSafeArea = true;
		Margin = new(0);
		Padding = new(0);

		this.Add(RemarksView, row: 1);
	}

	partial void OnContentChanged(View? oldValue, View? newValue)
	{
		if (oldValue is not null)
			this.Remove(oldValue);
		if (newValue is not null)
			this.Insert(0, newValue);
	}

	partial void OnRemarksDataChanged(IHasRemarksProperty? newValue)
	{
		RemarksView.RemarksData = newValue;
	}
}

