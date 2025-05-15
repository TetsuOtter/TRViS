using DependencyPropertyGenerator;

using TRViS.IO.Models;

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

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

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

	private void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		logger.Trace("SafeAreaMargin is changed: {0} -> {1}", Utils.ThicknessToString(oldValue), Utils.ThicknessToString(newValue));
#if IOS
		double bottomPaddingValue = newValue.Bottom;

		if (oldValue.Bottom == bottomPaddingValue)
		{
			logger.Trace("bottomPaddingValue is not changed -> do nothing");
			return;
		}

		if (bottomPaddingValue > 0)
		{
			logger.Debug("bottomPaddingValue is greater than 0 (= {0}) -> show BottomPaddingView", bottomPaddingValue);
			BottomPaddingView.IsVisible = true;
			RemarksAreaPaddingRowDefinition.Height = new(bottomPaddingValue, GridUnitType.Absolute);
		}
		else
		{
			RemarksAreaPaddingRowDefinition.Height = new(0, GridUnitType.Absolute);
			logger.Debug("bottomPaddingValue is less than or equal to 0 (= {0}) -> hide BottomPaddingView", bottomPaddingValue);
			BottomPaddingView.IsVisible = false;
		}
		RemarksView.ResetTextScrollViewPosition();

		RemarksAreaRowDefinition.Height = Remarks.HEADER_HEIGHT - bottomPaddingValue;
		RemarksView.BottomSafeAreaHeight = bottomPaddingValue;
		logger.Debug("Set RemarksAreaRowDefinition.Height to {0}", RemarksAreaRowDefinition.Height);
#endif
	}
}

