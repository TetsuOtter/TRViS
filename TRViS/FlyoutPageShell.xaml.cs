using System.Runtime.CompilerServices;

using TRViS.RootPages;
using TRViS.Services;
using TRViS.ViewModels;
using TRViS.DTAC;

namespace TRViS;

public partial class FlyoutPageShell : FlyoutPage
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

		InitializeComponent();

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

	private void OnSelectTrainClicked(object sender, EventArgs e)
	{
		logger.Trace("SelectTrain clicked");
		IsPresented = false;
		var page = new SelectTrainPage();
		Detail = new NavigationPage(page);
	}

	private void OnDTACClicked(object sender, EventArgs e)
	{
		logger.Trace("DTAC clicked");
		IsPresented = false;
		var page = new ViewHost();
		NavigationPage.SetHasNavigationBar(page, false);
		Detail = new NavigationPage(page);
	}

	private void OnLicensesClicked(object sender, EventArgs e)
	{
		logger.Trace("Licenses clicked");
		IsPresented = false;
		var page = new ThirdPartyLicenses();
		Detail = new NavigationPage(page);
	}

	private void OnSettingsClicked(object sender, EventArgs e)
	{
		logger.Trace("Settings clicked");
		IsPresented = false;
		var page = new EasterEggPage();
		Detail = new NavigationPage(page);
	}

	private void OnFirebaseSettingClicked(object sender, EventArgs e)
	{
		logger.Trace("Firebase Setting clicked");
		IsPresented = false;
		var page = new FirebaseSettingPage();
		Detail = new NavigationPage(page);
	}

	private void OnPrivacyClicked(object sender, EventArgs e)
	{
		logger.Trace("Privacy Policy clicked");
		IsPresented = false;
		var privacyPage = new ShowMarkdownPage() { Title = "Privacy Policy" };
		privacyPage.FileName = ResourceManager.AssetName.PrivacyPolicy_md;
		Detail = new NavigationPage(privacyPage);
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
