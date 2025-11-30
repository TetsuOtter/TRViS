using System.Runtime.CompilerServices;

using TRViS.RootPages;
using TRViS.Services;
using TRViS.ViewModels;
using TRViS.DTAC;

namespace TRViS;

public class FlyoutPageShell : FlyoutPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	static public string AppVersionString
		=> $"Version: {AppInfo.Current.VersionString}-{AppInfo.Current.BuildString}";

	readonly FirebaseSettingViewModel FirebaseSettingViewModel = InstanceManager.FirebaseSettingViewModel;
	readonly EasterEggPageViewModel EasterEggPageViewModel = InstanceManager.EasterEggPageViewModel;

	public FlyoutPageShell()
	{
		logger.Trace("FlyoutPageShell Creating");
		logger.Info("Application Version: {0}", AppVersionString);

		FlyoutLayoutBehavior = FlyoutLayoutBehavior.Popover;

		// Setup Flyout (Menu)
		SetupFlyout();

		// Setup Detail
		SetupDetail();

		// Setup Firebase if enabled
		if (FirebaseSettingViewModel.IsEnabled)
		{
			logger.Trace("Firebase Applying...");
			FirebaseSettingViewModel.SaveAndApplySettings(false);
		}

		// Set binding context
		this.BindingContext = EasterEggPageViewModel;

		// Subscribe to Firebase setting changes
		FirebaseSettingViewModel.IsEnabledChanged += (s, oldVal, newVal) =>
		{
			logger.Trace("Firebase enabled changed: {0} -> {1}", oldVal, newVal);
		};

		InstanceManager.AppViewModel.WindowWidth = DeviceDisplay.Current.MainDisplayInfo.Width;
		InstanceManager.AppViewModel.WindowHeight = DeviceDisplay.Current.MainDisplayInfo.Height;
		logger.Trace("Display Width/Height: {0}x{1}", InstanceManager.AppViewModel.WindowWidth, InstanceManager.AppViewModel.WindowHeight);

		logger.Trace("FlyoutPageShell Created");
	}

	private void SetDetailContent(ContentPage page)
	{
		logger.Trace("SetDetailContent: {0}", page?.GetType().Name ?? "null");
		if (page is not null)
		{
			// Wrap page in CustomNavigationPage with AppBar
			// Pass FlyoutPageShell reference so CustomNavigationPage can toggle menu
			// Show AppIcon button only for ViewHost
			bool showAppIconButton = page is ViewHost;
			var navPage = new CustomNavigationPage(page, (isShowing) =>
			{
				IsPresented = isShowing;
			}, showAppIconButton);

			// Set the title from the page if available
			if (!string.IsNullOrEmpty(page.Title))
			{
				navPage.AppBarTitle = page.Title;
			}

			Detail = navPage;
		}
	}
	void SetupFlyout()
	{
		var flyoutContent = new ContentPage
		{
			Title = "\xe241",
			BackgroundColor = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#EEE") : Color.FromArgb("#111"),
			Content = new StackLayout
			{
				Padding = new Thickness(20),
				Spacing = 10,
				Children =
				{
					new Button
					{
						Text = "Select Train",
						BackgroundColor = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#0066CC") : Color.FromArgb("#0088FF"),
						TextColor = Colors.White,
						Padding = new Thickness(10),
					},
					new Button
					{
						Text = "D-TAC",
						BackgroundColor = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#0066CC") : Color.FromArgb("#0088FF"),
						TextColor = Colors.White,
						Padding = new Thickness(10),
					},
					new Button
					{
						Text = "Third Party Licenses",
						BackgroundColor = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#0066CC") : Color.FromArgb("#0088FF"),
						TextColor = Colors.White,
						Padding = new Thickness(10),
					},
					new Button
					{
						Text = "Settings",
						BackgroundColor = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#0066CC") : Color.FromArgb("#0088FF"),
						TextColor = Colors.White,
						Padding = new Thickness(10),
					},
					new Button
					{
						Text = "Firebase Setting",
						BackgroundColor = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#0066CC") : Color.FromArgb("#0088FF"),
						TextColor = Colors.White,
						Padding = new Thickness(10),
					},
					new Button
					{
						Text = "Privacy Policy",
						BackgroundColor = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#0066CC") : Color.FromArgb("#0088FF"),
						TextColor = Colors.White,
						Padding = new Thickness(10),
					},
					new BoxView
					{
						HeightRequest = 1,
						Color = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#CCC") : Color.FromArgb("#333"),
						Margin = new Thickness(0, 10)
					},
					new Label
					{
						Text = AppVersionString,
						FontSize = 12,
						TextColor = AppTheme.Light == Application.Current?.UserAppTheme ? Color.FromArgb("#666") : Color.FromArgb("#AAA")
					}
				}
			}
		};

		// Wire up button click handlers
		var buttons = (flyoutContent.Content as StackLayout)?.Children.OfType<Button>().ToList();
		if (buttons != null && buttons.Count >= 6)
		{
			buttons[0].Clicked += (s, e) => OnSelectTrainClicked(s ?? this, e ?? new EventArgs());
			buttons[1].Clicked += (s, e) => OnDTACClicked(s ?? this, e ?? new EventArgs());
			buttons[2].Clicked += (s, e) => OnLicensesClicked(s ?? this, e ?? new EventArgs());
			buttons[3].Clicked += (s, e) => OnSettingsClicked(s ?? this, e ?? new EventArgs());
			buttons[4].Clicked += (s, e) => OnFirebaseSettingClicked(s ?? this, e ?? new EventArgs());
			buttons[5].Clicked += (s, e) => OnPrivacyClicked(s ?? this, e ?? new EventArgs());
		}

		Flyout = flyoutContent;
	}

	void SetupDetail()
	{
		// Set initial Detail with SelectTrainPage
		SetDetailContent(new SelectTrainPage());
	}

	private void OnSelectTrainClicked(object sender, EventArgs e)
	{
		logger.Trace("SelectTrain clicked");
		IsPresented = false;
		SetDetailContent(new SelectTrainPage());
	}

	private void OnDTACClicked(object sender, EventArgs e)
	{
		logger.Trace("DTAC clicked");
		IsPresented = false;
		SetDetailContent(new ViewHost());
	}

	private void OnLicensesClicked(object sender, EventArgs e)
	{
		logger.Trace("Licenses clicked");
		IsPresented = false;
		SetDetailContent(new ThirdPartyLicenses());
	}

	private void OnSettingsClicked(object sender, EventArgs e)
	{
		logger.Trace("Settings clicked");
		IsPresented = false;
		SetDetailContent(new EasterEggPage());
	}

	private void OnFirebaseSettingClicked(object sender, EventArgs e)
	{
		logger.Trace("Firebase Setting clicked");
		IsPresented = false;
		SetDetailContent(new FirebaseSettingPage());
	}

	private void OnPrivacyClicked(object sender, EventArgs e)
	{
		logger.Trace("Privacy Policy clicked");
		IsPresented = false;
		var privacyPage = new ShowMarkdownPage() { Title = "Privacy Policy" };
		privacyPage.FileName = ResourceManager.AssetName.PrivacyPolicy_md;
		SetDetailContent(privacyPage);
	}

	protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);

		switch (propertyName)
		{
			case nameof(Width):
				logger.Trace("Width: {0}", Width);
				InstanceManager.AppViewModel.WindowWidth = Width;
				break;
			case nameof(Height):
				logger.Trace("Height: {0}", Height);
				InstanceManager.AppViewModel.WindowHeight = Height;
				break;
		}
	}

	protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
	{
		InstanceManager.AppViewModel.WindowWidth = widthConstraint;
		InstanceManager.AppViewModel.WindowHeight = heightConstraint;
		logger.Trace("MeasureOverride: {0}x{1}", widthConstraint, heightConstraint);
		return base.MeasureOverride(widthConstraint, heightConstraint);
	}
}

