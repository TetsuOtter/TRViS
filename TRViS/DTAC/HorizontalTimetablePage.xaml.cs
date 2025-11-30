using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class HorizontalTimetablePage : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public const string NameOfThisClass = nameof(HorizontalTimetablePage);

	readonly GradientStop TitleBG_Top = new(Colors.White.WithAlpha(0.8f), 0);
	readonly GradientStop TitleBG_Middle = new(Colors.White.WithAlpha(0.5f), 0.5f);
	readonly GradientStop TitleBG_MidBottom = new(Colors.White.WithAlpha(0.1f), 0.8f);
	readonly GradientStop TitleBG_Bottom = new(Colors.White.WithAlpha(0), 1);

	public HorizontalTimetablePage()
	{
		logger.Trace("Creating...");

		AppViewModel vm = InstanceManager.AppViewModel;
		EasterEggPageViewModel eevm = InstanceManager.EasterEggPageViewModel;

		Shell.SetNavBarIsVisible(this, false);

		InitializeComponent();

		TitleLabel.TextColor
			= BackButton.TextColor
			= eevm.ShellTitleTextColor;

		TitleBGBoxView.SetBinding(BoxView.ColorProperty, BindingBase.Create(static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor, source: eevm));

		TitleBGGradientBox.Color = null;
		TitleBGGradientBox.Background = new LinearGradientBrush(new GradientStopCollection()
		{
			TitleBG_Top,
			TitleBG_Middle,
			TitleBG_MidBottom,
			TitleBG_Bottom,
		},
		new Point(0, 0),
		new Point(0, 1));

		vm.CurrentAppThemeChanged += (s, e) => SetTitleBGGradientColor(e.NewValue);
		SetTitleBGGradientColor(vm.CurrentAppTheme);
		eevm.PropertyChanged += Eevm_PropertyChanged;

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		LoadContent(vm.SelectedWork);

		logger.Trace("Created");
	}

	void SetTitleBGGradientColor(AppTheme v)
		=> SetTitleBGGradientColor(v == AppTheme.Dark ? Colors.Black : Colors.White);
	void SetTitleBGGradientColor(Color v)
	{
		logger.Debug("newValue: {0}", v);
		TitleBG_Top.Color = v.WithAlpha(0.8f);
		TitleBG_Middle.Color = v.WithAlpha(0.5f);
		TitleBG_MidBottom.Color = v.WithAlpha(0.1f);
		TitleBG_Bottom.Color = v.WithAlpha(0);
	}

	private void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		double top = newValue.Top;
		if (oldValue.Top == top
			&& oldValue.Left == newValue.Left
			&& oldValue.Right == newValue.Right)
		{
			logger.Trace("SafeAreaMargin is not changed -> do nothing");
			return;
		}

		TitleBGGradientBox.Margin = new(-newValue.Left, -top, -newValue.Right, ViewHost.TITLE_VIEW_HEIGHT * 0.5);
		TitlePaddingViewHeight.Height = new(top, GridUnitType.Absolute);
		BackButton.Margin = new(8 + newValue.Left, 4);
		logger.Debug("SafeAreaMargin is changed -> set TitleBGGradientBox.Margin to {0}", Utils.ThicknessToString(TitleBGGradientBox.Margin));
	}

	private void Eevm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (sender is not EasterEggPageViewModel vm)
			return;

		switch (e.PropertyName)
		{
			case nameof(EasterEggPageViewModel.ShellTitleTextColor):
				logger.Trace("ShellTitleTextColor is changed to {0}", vm.ShellTitleTextColor);
				TitleLabel.TextColor
					= BackButton.TextColor
					= vm.ShellTitleTextColor;
				break;
		}
	}

	private void BackButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("BackButton_Clicked -> GoBack");
		Shell.Current.GoToAsync("..");
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

		ContentType contentType = (ContentType)(work.ETrainTimetableContentType ?? 0);
		byte[] content = work.ETrainTimetableContent;

		logger.Debug("Content type: {0}, Content length: {1}", contentType, content.Length);

		switch (contentType)
		{
			case ContentType.PNG:
			case ContentType.JPG:
				LoadImageContent(content, contentType);
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

	void LoadImageContent(byte[] content, ContentType contentType)
	{
		logger.Info("Loading image content, type: {0}", contentType);
		ContentWebView.IsVisible = false;
		ContentImage.IsVisible = true;
		ContentImage.Source = ImageSource.FromStream(() => new MemoryStream(content));
	}

	void LoadPdfContent(byte[] content)
	{
		logger.Info("Loading PDF content");
		ContentWebView.IsVisible = true;
		ContentImage.IsVisible = false;

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
		ContentWebView.IsVisible = true;
		ContentImage.IsVisible = false;

		string uri = System.Text.Encoding.UTF8.GetString(content);
		logger.Debug("URI: {0}", uri);
		ContentWebView.Source = uri;
	}
}
