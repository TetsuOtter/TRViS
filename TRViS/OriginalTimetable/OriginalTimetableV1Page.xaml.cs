using TRViS.OriginalTimetable.Resources;
using TRViS.Services;

namespace TRViS.OriginalTimetable;

// V1 Modern Classic — 独自時刻表ページ。幅 768pt 閾値でタブレット/コンパクト切替。
public partial class OriginalTimetableV1Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV1Page);

	private const double TabletBreakpoint = 768;
	private bool? _lastIsTablet;

	public OriginalTimetableV1Page()
	{
		InitializeComponent();
		BindingContext = InstanceManager.OriginalTimetableViewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.Portrait);
		// 初期描画 — SizeChanged が来ない場合に備えて
		SwapLayoutIfNeeded(Width);
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
	}

	void OnRootSizeChanged(object? sender, EventArgs e)
	{
		if (sender is VisualElement ve)
			SwapLayoutIfNeeded(ve.Width);
	}

	private void SwapLayoutIfNeeded(double width)
	{
		if (width <= 0)
			return;
		bool isTablet = width >= TabletBreakpoint;
		if (_lastIsTablet == isTablet)
			return;
		_lastIsTablet = isTablet;

		RootGrid.Children.Clear();
		if (isTablet)
		{
			RootGrid.Children.Add(new V1TabletLayout
			{
				BindingContext = BindingContext,
			});
		}
		else
		{
			// TODO(next slice): compact (<768pt) レイアウト実装
			RootGrid.Children.Add(new Label
			{
				Text = "V1 Compact レイアウトは次タスクで実装予定",
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
				HorizontalTextAlignment = TextAlignment.Center,
			});
		}
	}
}
