using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Microsoft.Maui.Controls.Shapes;

using TRViS.IO.RequestInfo;
using TRViS.Services;
using TRViS.Utils;

using IOSPage = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page;

namespace TRViS.RootPages;

/// <summary>
/// Modal page used by Start/Home → "サーバーから読み込み". Two-state UI: a
/// rich-card history list (one card per past URL) and a new-connection
/// form with a "接続先を保存する" toggle that gates whether the URL is
/// added to <see cref="ViewModels.AppViewModel.ExternalResourceUrlHistory"/>.
/// </summary>
public partial class ConnectServerDialog : ContentPage
{
	internal const string AutomationId_HistoryItemPrefix = "ConnectServer.HistoryItem.";

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public ConnectServerDialog()
	{
		logger.Trace("Creating");
		InitializeComponent();

		// iPad / Mac Catalyst: present as a centered FormSheet so a few-line URL
		// form doesn't take the whole screen. iPhone falls back to fullscreen
		// automatically because UIKit ignores FormSheet on compact widths.
		IOSPage.SetModalPresentationStyle(this.On<iOS>(), UIModalPresentationStyle.FormSheet);

		// Set initial sub-view visibility BEFORE first paint based on whether the
		// app already has URL history. Both views default IsVisible=true in XAML
		// (so each gets a UIA peer on Windows, where IsVisible='False' XAML
		// defaults skip peer creation and miss the runtime flip), but they
		// occupy the same Grid.Row and would visually overlap if both stayed
		// visible. Hiding the inactive one synchronously from the constructor
		// avoids any first-paint frame where both render simultaneously.
		PopulateHistory();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// Re-populate on each appearance so a re-show after preferences changed
		// (e.g., user added a URL elsewhere) reflects the latest history.
		PopulateHistory();
	}

	void PopulateHistory()
	{
		var urls = InstanceManager.AppViewModel.ExternalResourceUrlHistory.Reverse().ToList();

		HistoryListContainer.Children.Clear();

		if (urls.Count == 0)
		{
			// No history -> jump straight to the new-connection form. The
			// "+ 新規接続" / back-to-history affordances stay hidden so the
			// dialog reads as a single-purpose form.
			ShowNewConnectionView(showBackButton: false);
			NewConnectionButton.IsVisible = false;
			return;
		}

		foreach (string url in urls)
		{
			HistoryListContainer.Children.Add(CreateHistoryCard(url));
		}

		ShowHistoryView();
	}

	View CreateHistoryCard(string url)
	{
		// Parse the scheme so we can show a Material icon + host as the primary
		// line. Falls back to the raw URL if parsing fails (defensive: a
		// malformed stored entry shouldn't crash the dialog).
		// Glyphs come from MaterialIcons-Regular.ttf (registered in MauiProgram
		// as "MaterialIconsRegular"):
		//   \uE894 = language  (globe — http/https web fetch)
		//   \uE3E7 = flash_on  (lightning — ws/wss realtime stream)
		//   \uE324 = phone_iphone (trvis:// app deeplink)
		//   \uE157 = link      (fallback — unknown scheme)
		string glyph;
		string title;
		// UriKind.Absolute so a malformed stored entry actually trips the catch;
		// the previous RelativeOrAbsolute swallowed everything (catch was dead code).
		try
		{
			Uri uri = new(url, UriKind.Absolute);
			string scheme = uri.Scheme;
			glyph = scheme switch
			{
				"https" or "http" => "\uE894",
				"wss" or "ws" => "\uE3E7",
				"trvis" => "\uE324",
				_ => "\uE157",
			};
			title = !string.IsNullOrEmpty(uri.Host) ? uri.Host : url;
		}
		catch
		{
			glyph = "\uE157";
			title = url;
		}

		var border = new Border
		{
			Style = (Style)Resources["HistoryCard"],
			AutomationId = AutomationId_HistoryItemPrefix + url,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
		};

		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition { Width = GridLength.Auto },
				new ColumnDefinition { Width = GridLength.Star },
			},
			ColumnSpacing = 12,
			RowDefinitions =
			{
				new RowDefinition { Height = GridLength.Auto },
				new RowDefinition { Height = GridLength.Auto },
			},
		};

		var glyphLabel = new Label
		{
			Text = glyph,
			FontFamily = "MaterialIconsRegular",
			FontSize = 28,
			VerticalOptions = LayoutOptions.Center,
		};
		RootStyles.TableTextColor.Apply(glyphLabel, Label.TextColorProperty);
		Grid.SetColumn(glyphLabel, 0);
		Grid.SetRowSpan(glyphLabel, 2);
		grid.Children.Add(glyphLabel);

		var titleLabel = new Label
		{
			Text = title,
			FontSize = 15,
			FontAttributes = FontAttributes.Bold,
			LineBreakMode = LineBreakMode.TailTruncation,
		};
		RootStyles.TableTextColor.Apply(titleLabel, Label.TextColorProperty);
		Grid.SetColumn(titleLabel, 1);
		Grid.SetRow(titleLabel, 0);
		grid.Children.Add(titleLabel);

		var subtitleLabel = new Label
		{
			Text = url,
			FontSize = 12,
			LineBreakMode = LineBreakMode.TailTruncation,
		};
		RootStyles.TableDetailColor.Apply(subtitleLabel, Label.TextColorProperty);
		Grid.SetColumn(subtitleLabel, 1);
		Grid.SetRow(subtitleLabel, 1);
		grid.Children.Add(subtitleLabel);

		border.Content = grid;

		// Card-tap loads directly. No shared Entry to populate, so this
		// avoids the SelectionChanged↔TextChanged re-entrancy that the
		// legacy popup had to guard against.
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, __) => await LoadFromHistoryAsync(url);
		border.GestureRecognizers.Add(tap);

		return border;
	}

	void ShowHistoryView()
	{
		HistoryView.IsVisible = true;
		NewConnectionView.IsVisible = false;
		NewConnectionButton.IsVisible = true;
	}

	void ShowNewConnectionView(bool showBackButton)
	{
		HistoryView.IsVisible = false;
		NewConnectionView.IsVisible = true;
		NewConnectionButton.IsVisible = false;
		BackToHistoryButton.IsVisible = showBackButton;
	}

	void SetInputEnabled(bool isEnabled)
	{
		UrlInput.IsEnabled = isEnabled;
		ConnectButton.IsEnabled = isEnabled;
		SaveConnectionSwitch.IsEnabled = isEnabled;
		NewConnectionButton.IsEnabled = isEnabled;
		BackToHistoryButton.IsEnabled = isEnabled;
		LoadingIndicator.IsRunning = !isEnabled;
		LoadingIndicator.IsVisible = !isEnabled;
	}

	async Task LoadFromHistoryAsync(string url)
	{
		logger.Info("Loading from history: {0}", url);
		try
		{
			SetInputEnabled(false);
			// History tap always re-saves (bumps to top). The "save connection"
			// toggle is a property of the new-connection form only — toggling
			// it during a history tap could let users accidentally remove
			// already-saved entries, which the spec does not call for.
			bool ok = await TryLoadAsync(url, addToHistory: true);
			if (ok)
				await Navigation.PopModalAsync();
		}
		finally
		{
			SetInputEnabled(true);
		}
	}

	async Task<bool> TryLoadAsync(string urlText, bool addToHistory)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(urlText))
			{
				await Util.DisplayAlertAsync("接続できませんでした", "URLを入力してください。", "OK");
				return false;
			}

			if (urlText.StartsWith("trvis://"))
			{
				return await InstanceManager.AppViewModel.HandleAppLinkUriAsync(urlText, addToHistory, CancellationToken.None);
			}

			AppLinkInfo appLinkInfo = new(
				AppLinkInfo.FileType.Json,
				Version: new(1, 0),
				ResourceUri: new(urlText)
			);
			return await InstanceManager.AppViewModel.HandleAppLinkUriAsync(appLinkInfo, addToHistory, CancellationToken.None);
		}
		catch (UriFormatException ex)
		{
			logger.Warn(ex, "Invalid URL: {0}", urlText);
			await Util.DisplayAlertAsync("接続できませんでした", "URLの形式が正しくありません。", "OK");
			return false;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TryLoadAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "ConnectServerDialog.TryLoadAsync");
			return false;
		}
	}

	// ----- Event handlers -----

	async void OnCloseClicked(object sender, EventArgs e)
	{
		logger.Trace("Close clicked");
		try
		{
			await Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PopModalAsync failed");
		}
	}

	void OnNewConnectionClicked(object sender, EventArgs e)
	{
		logger.Trace("New connection clicked");
		// Show back button only when there's history to go back to.
		bool hasHistory = HistoryListContainer.Children.Count > 0;
		ShowNewConnectionView(showBackButton: hasHistory);
	}

	void OnBackToHistoryClicked(object sender, EventArgs e)
	{
		logger.Trace("Back to history clicked");
		ShowHistoryView();
	}

	async void OnUrlInputCompleted(object sender, EventArgs e)
	{
		await SubmitNewConnectionAsync();
	}

	async void OnConnectClicked(object sender, EventArgs e)
	{
		await SubmitNewConnectionAsync();
	}

	async Task SubmitNewConnectionAsync()
	{
		// Trim before submit: clipboard paste on mobile commonly carries a leading/trailing
		// whitespace which makes new Uri(...) throw UriFormatException.
		string urlText = (UrlInput.Text ?? string.Empty).Trim();
		bool addToHistory = SaveConnectionSwitch.IsToggled;
		logger.Info("Connect clicked: addToHistory={0}", addToHistory);

		try
		{
			SetInputEnabled(false);
			bool ok = await TryLoadAsync(urlText, addToHistory);
			if (ok)
				await Navigation.PopModalAsync();
		}
		finally
		{
			SetInputEnabled(true);
		}
	}
}
