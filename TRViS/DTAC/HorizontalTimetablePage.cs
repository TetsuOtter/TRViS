using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

/// <summary>
/// Page for displaying horizontal timetable content (PDF/PNG/JPG/URI) in a WebView.
/// Implemented entirely in C# without XAML.
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

		// Create the main grid layout
		var mainGrid = new Grid
		{
			IgnoreSafeArea = true,
			RowDefinitions = new RowDefinitionCollection
			{
				new RowDefinition(GridLength.Auto), // AppBar
				new RowDefinition(GridLength.Star)  // Content
			}
		};

		// Create AppBar with back button
		AppBarView = new AppBar
		{
			Title = "横型時刻表",
			LeftButtonText = "\ue5c4" // Back arrow icon
		};
		AppBarView.LeftButtonClicked += BackButton_Clicked;
		Grid.SetRow(AppBarView, 0);
		mainGrid.Children.Add(AppBarView);

		// Create WebView for content
		ContentWebView = new WebView();
		Grid.SetRow(ContentWebView, 1);
		mainGrid.Children.Add(ContentWebView);

		Content = mainGrid;

		// Subscribe to safe area margin changes
		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		LoadContent(vm.SelectedWork);

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

	void LoadContent(Work? work)
	{
		if (work is null)
		{
			logger.Warn("Work is null -> do nothing");
			return;
		}

		logger.Info("Loading horizontal timetable content for work: {0}", work.Id);

		if (work.HasETrainTimetable != true || work.ETrainTimetableContent is null)
		{
			logger.Warn("No horizontal timetable content available");
			return;
		}

		int contentTypeValue = work.ETrainTimetableContentType ?? (int)ContentType.PNG;
		if (!Enum.IsDefined(typeof(ContentType), contentTypeValue))
		{
			logger.Warn("Unknown content type value: {0}, defaulting to PNG", contentTypeValue);
			contentTypeValue = (int)ContentType.PNG;
		}
		ContentType contentType = (ContentType)contentTypeValue;
		byte[] content = work.ETrainTimetableContent;

		logger.Debug("Content type: {0}, Content length: {1}", contentType, content.Length);

		// Always use WebView for all content types
		switch (contentType)
		{
			case ContentType.PNG:
				LoadImageContent(content, "image/png");
				break;
			case ContentType.JPG:
				LoadImageContent(content, "image/jpeg");
				break;
			case ContentType.PDF:
				LoadPdfContent(content);
				break;
			case ContentType.URI:
				LoadUriContent(content);
				break;
			default:
				logger.Warn("Unsupported content type: {0}", contentType);
				break;
		}
	}

	void LoadImageContent(byte[] content, string mimeType)
	{
		logger.Info("Loading image content via WebView, mimeType: {0}", mimeType);

		// Convert image to base64 data URI and display in WebView
		string base64Content = Convert.ToBase64String(content);
		string dataUri = $"data:{mimeType};base64,{base64Content}";

		string html = $@"
<!DOCTYPE html>
<html>
<head>
	<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, user-scalable=yes"">
	<style>
		html, body {{ margin: 0; padding: 0; height: 100%; width: 100%; background-color: transparent; }}
		body {{ display: flex; justify-content: center; align-items: center; }}
		img {{ max-width: 100%; max-height: 100%; object-fit: contain; }}
	</style>
</head>
<body>
	<img src=""{dataUri}"" alt=""Horizontal Timetable"" />
</body>
</html>";

		ContentWebView.Source = new HtmlWebViewSource { Html = html };
	}

	void LoadPdfContent(byte[] content)
	{
		logger.Info("Loading PDF content");

		// Convert PDF to data URI for WebView
		string base64Content = Convert.ToBase64String(content);
		string dataUri = $"data:application/pdf;base64,{base64Content}";

		// Use an HTML page to embed the PDF
		string html = $@"
<!DOCTYPE html>
<html>
<head>
	<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
	<style>
		body {{ margin: 0; padding: 0; }}
		embed, iframe {{ width: 100%; height: 100%; border: none; }}
	</style>
</head>
<body>
	<embed src=""{dataUri}"" type=""application/pdf"" />
</body>
</html>";

		ContentWebView.Source = new HtmlWebViewSource { Html = html };
	}

	void LoadUriContent(byte[] content)
	{
		logger.Info("Loading URI content");

		string uri = System.Text.Encoding.UTF8.GetString(content);
		logger.Debug("URI: {0}", uri);
		ContentWebView.Source = uri;
	}
}
