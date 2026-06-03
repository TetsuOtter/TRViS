using TRViS.RootPages;

namespace TRViS.OriginalTimetable;

// Minimal smoke-test page for the Android push-route navigation pattern
// (MAUI #16927 mitigation). Navigate here, navigate away, come back: the
// page must NOT show a blank white screen on the second visit.
// Once confirmed on Android, replace this stub with the real V1 layout.
public class OriginalTimetableSimplePage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableSimplePage);

	// Mirrors AutomationIds.DTAC.TestNavigateHomeButton in the test project
	// so AppShellPage.NavigateToHome() works from this page without changes.
	private const string AutomationIdValueForTestNavigateHome = "DTAC.TestNavigateHomeButton";
	internal const string AutomationIdPageLabel = "OriginalTimetable.Simple.PageLabel";
	internal const string AutomationIdRoot = "OriginalTimetable.Simple.Root";

	public OriginalTimetableSimplePage()
	{
		Title = "ダイヤ表 (テスト)";
		Shell.SetNavBarIsVisible(this, false);

		var label = new Label
		{
			AutomationId = AutomationIdPageLabel,
			Text = "ダイヤ表テストページ\n\nこのページに複数回ナビゲートしても\n白画面にならないことを確認してください",
			VerticalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.Center,
			Padding = new Thickness(24),
		};

		Content = new Grid
		{
			AutomationId = AutomationIdRoot,
			Children = { label },
		};

#if UI_TEST
		AddTestSeamButtons();
#endif
	}

#if UI_TEST
	private void AddTestSeamButtons()
	{
		// Transparent 24×24 button that issues GoToAsync("//StartHomePage").
		// AutomationId mirrors DTAC.TestNavigateHomeButton so the existing
		// AppShellPage.NavigateToHome() seam-first search finds it here too.
		var btn = new Button
		{
			AutomationId = AutomationIdValueForTestNavigateHome,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			BackgroundColor = Colors.Transparent,
			BorderColor = Colors.Transparent,
			Padding = 0,
		};
		btn.Clicked += async (_, _) =>
		{
			try { await Shell.Current.GoToAsync("//" + StartHomePage.NameOfThisClass); }
			catch { }
		};
		((Grid)Content!).Children.Add(btn);
	}
#endif
}
