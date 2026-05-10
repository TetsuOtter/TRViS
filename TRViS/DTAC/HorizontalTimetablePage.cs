using TRViS.DTAC.Logic.Formatters;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

/// <summary>
/// Page for displaying horizontal timetable content (PDF/PNG/JPG/URI) in a WebView.
/// Rendering payload is built by <see cref="HorizontalTimetableContentBuilder"/>;
/// this class only owns the MAUI surface.
/// </summary>
public class HorizontalTimetablePage : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public const string NameOfThisClass = nameof(HorizontalTimetablePage);

	readonly AppBar AppBarView;
	readonly WebView ContentWebView;

	public HorizontalTimetablePage()
	{
		logger.Trace("Creating...");

		AppViewModel vm = InstanceManager.AppViewModel;

		Shell.SetNavBarIsVisible(this, false);
		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		var mainGrid = new Grid
		{
			SafeAreaEdges = SafeAreaEdges.None,
			RowDefinitions = new RowDefinitionCollection
			{
				new RowDefinition(GridLength.Auto),
				new RowDefinition(GridLength.Star),
			},
		};

		AppBarView = new AppBar
		{
			Title = "横型時刻表",
			LeftButtonText = DTACElementStyles.BackArrowIcon,
		};
		AppBarView.LeftButtonClicked += BackButton_Clicked;
		Grid.SetRow(AppBarView, 0);
		mainGrid.Children.Add(AppBarView);

		ContentWebView = new WebView
		{
			AutomationId = "HorizontalTimetable.WebView",
		};
		Grid.SetRow(ContentWebView, 1);
		mainGrid.Children.Add(ContentWebView);

		Content = mainGrid;

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		ApplyContent(HorizontalTimetableContentBuilder.Build(vm.SelectedWork));

		logger.Trace("Created");
	}

	private void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		AppBarView.UpdateSafeAreaMargin(oldValue, newValue);
	}

	private async void BackButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("BackButton_Clicked -> GoBack with animation");
		await Shell.Current.GoToAsync("..", true);
	}

	void ApplyContent(HorizontalTimetableRenderResult result)
	{
		logger.Info("ApplyContent: kind={0}", result.Kind);
		switch (result.Kind)
		{
			case HorizontalTimetableRenderKind.Html:
				ContentWebView.Source = new HtmlWebViewSource { Html = result.Payload };
				break;
			case HorizontalTimetableRenderKind.Uri:
				ContentWebView.Source = result.Payload;
				break;
			case HorizontalTimetableRenderKind.None:
			default:
				logger.Warn("No horizontal timetable content available");
				break;
		}
	}
}
