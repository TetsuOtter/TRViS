using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC;

[ContentProperty(nameof(Content))]
[DependencyProperty<View>("Content")]
[DependencyProperty<IHasRemarksProperty>("RemarksData")]
public partial class WithRemarksView : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	Remarks RemarksView { get; } = new();
	RowDefinition RemarksAreaRowDefinition { get; } = new(new(Remarks.HEADER_HEIGHT, GridUnitType.Absolute));

#if IOS
	RowDefinition RemarksAreaPaddingRowDefinition { get; } = new(new(0, GridUnitType.Absolute));
	BoxView BottomPaddingView { get; } = new()
	{
		Color = new(0x33, 0x33, 0x33),
	};
#endif

	public bool IsOpen
	{
		get => RemarksView.IsOpen;
		set => RemarksView.IsOpen = value;
	}

	public WithRemarksView()
	{
		logger.Trace("Creating...");

		RowDefinitions.Add(new(new(1, GridUnitType.Star)));
		RowDefinitions.Add(RemarksAreaRowDefinition);

		IgnoreSafeArea = true;
		Margin = new(0);
		Padding = new(0);

		// FlyoutPage doesn't have SafeAreaMargin event like AppShell
		// SafeArea handling is simplified for FlyoutPage architecture

#if IOS
		RowDefinitions.Add(RemarksAreaPaddingRowDefinition);
		this.Add(BottomPaddingView, row: 2);
		logger.Trace("Added BottomPaddingView");
#endif

		this.Add(RemarksView, row: 1);

		logger.Trace("Created");
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
		logger.Trace("RemarksData is changed to {0}", newValue?.Remarks);
		RemarksView.RemarksData = newValue;
	}
}

