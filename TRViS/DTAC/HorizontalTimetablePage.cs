using TRViS.DTAC.Adapters;
using TRViS.DTAC.Logic.Formatters;
using TRViS.DTAC.Logic.Presenter;
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

	readonly ViewHostPresenter _presenter;
	readonly AppBar AppBarView;
	readonly WebView ContentWebView;

	public HorizontalTimetablePage()
	{
		logger.Trace("Creating...");

		_presenter = PresenterFactory.BuildViewHostPresenter(out AppViewModel vm, out _, out _);
		_presenter.StateChanged += OnPresenterStateChanged;
		Unloaded += (_, _) => _presenter.Dispose();

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
			Title = _presenter.CurrentState.TitleText,
			LeftButtonText = DTACElementStyles.BackArrowIcon,
			LeftButtonAutomationId = "HorizontalTimetable.BackButton",
			TimeLabelText = _presenter.CurrentState.TimeLabelText,
			IsTimeLabelEnabled = true,
			IsThemeButtonEnabled = true,
			IsAppIconButtonEnabled = false,
		};
		AppBarView.LeftButtonClicked += BackButton_Clicked;
		Grid.SetRow(AppBarView, 0);
		mainGrid.Children.Add(AppBarView);

		// iOS は WebView 自体に AutomationId を載せれば WKWebView の accessibility
		// identifier に伝わるが、macCatalyst の Mac2Driver では伝わらず AccessibilityId
		// で掴めない (経験的に確認済み)。ラッパー Grid 側に AutomationId を載せると
		// iOS / macOS どちらも AccessibilityId で取れるようになる。Android (UIA2) と
		// Windows (WinUI3) では AutomationId を Grid に置いても resource-id /
		// AutomationId に伝わらない (Windows は non-control Pane として現れる) ため、
		// PageObject 側でネイティブ WebView の class 名 (android.webkit.WebView /
		// Microsoft.UI.Xaml.Controls.WebView2) を XPath / By.ClassName でフォール
		// バックしている (HorizontalTimetablePageObject.cs)。
		ContentWebView = new WebView();
		var webViewHost = new Grid
		{
			AutomationId = "HorizontalTimetable.WebView",
		};
		webViewHost.Children.Add(ContentWebView);
		Grid.SetRow(webViewHost, 1);
		mainGrid.Children.Add(webViewHost);

		Content = mainGrid;

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		_ = ApplyContentAsync(HorizontalTimetableContentBuilder.Build(vm.SelectedWork));

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

	private void OnPresenterStateChanged(object? sender, ViewHostStateChangedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var state = _presenter.CurrentState;
			if ((e.Changed & ViewHostStateSection.TitleText) != 0)
				AppBarView.Title = state.TitleText;
			if ((e.Changed & ViewHostStateSection.TimeLabel) != 0)
				AppBarView.TimeLabelText = state.TimeLabelText;
		});
	}

	async Task ApplyContentAsync(HorizontalTimetableRenderResult result)
	{
		logger.Info("ApplyContent: kind={0}", result.Kind);
		switch (result.Kind)
		{
			case HorizontalTimetableRenderKind.Png:
				ContentWebView.Source = new HtmlWebViewSource { Html = HorizontalTimetableImageHtmlBuilder.BuildPng(result.Payload) };
				break;
			case HorizontalTimetableRenderKind.Jpg:
				ContentWebView.Source = new HtmlWebViewSource { Html = HorizontalTimetableImageHtmlBuilder.BuildJpg(result.Payload) };
				break;
			case HorizontalTimetableRenderKind.Uri:
				ContentWebView.Source = result.Payload;
				break;
			case HorizontalTimetableRenderKind.Pdf:
				try
				{
					var pdfEngine = InstanceManager.EasterEggPageViewModel.PdfJsRenderEngine;
					string html = await PdfJsViewerHtmlBuilder.BuildAsync(result.Payload, pdfEngine).ConfigureAwait(true);
					ContentWebView.Source = new HtmlWebViewSource { Html = html };
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Failed to build PDF.js viewer HTML");
				}
				break;
			case HorizontalTimetableRenderKind.None:
			default:
				logger.Warn("No horizontal timetable content available");
				break;
		}
	}
}
