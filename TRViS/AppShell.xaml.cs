#if IOS
using System.Runtime.Versioning;
#endif

using System.Runtime.CompilerServices;

using TRViS.DTAC;
using TRViS.FirebaseWrapper;
using TRViS.IO.Models;
using TRViS.Localization;
using TRViS.RootPages;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS;

public partial class AppShell : Shell
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	static public string AppVersionString
		=> $"Version: {AppInfo.Current.VersionString}-{AppInfo.Current.BuildString}";

	readonly FirebaseSettingViewModel FirebaseSettingViewModel = InstanceManager.FirebaseSettingViewModel;

	public AppShell()
	{
		logger.Trace("AppShell Creating");

		Routing.RegisterRoute(HorizontalTimetablePage.NameOfThisClass, typeof(HorizontalTimetablePage));
		logger.Info("Application Version: {0}", AppVersionString);

		EasterEggPageViewModel easterEggPageViewModel = InstanceManager.EasterEggPageViewModel;

		logger.Trace("Checking Firebase Setting");
		if (FirebaseSettingViewModel.IsEnabled)
		{
			logger.Trace("Firebase Applying...");
			FirebaseSettingViewModel.SaveAndApplySettings(false);
		}

		InitializeComponent();

		// Flyout/MenuItem Title binding refresh is unreliable in MAUI Shell, so
		// set them imperatively now and again whenever the UI language changes.
		ApplyLocalization();
		LocalizationResourceManager.Current.CultureChanged += (_, _) =>
			MainThread.BeginInvokeOnMainThread(ApplyLocalization);

		// Always launch into the Start/Home page. The Start screen handles the
		// privacy-policy-not-accepted case via an in-page banner + modal dialog
		// (PrivacyPolicyDialog), which also hosts the Firebase analytics opt-in.
		// The dedicated FirebaseSettingPage / Privacy / TPL flyout entries were
		// removed since Home now covers all three.
		// Fire-and-forget: the Shell ctor cannot be async; we discard the Task and
		// log via continuation so a navigation failure doesn't vanish.
		_ = GoToAsync("//" + nameof(StartHomePage)).ContinueWith(t =>
		{
			if (t.IsFaulted)
				logger.Error(t.Exception, "Initial GoToAsync(StartHomePage) failed");
		}, TaskScheduler.Default);
		InstanceManager.AnalyticsWrapper.Log(AnalyticsEvents.AppLaunched);

		// Always start with the flyout enabled. On Mac Catalyst the navigation
		// bar / flyout toggle button is created during Shell initialization based
		// on the current FlyoutBehavior — switching from Disabled→Flyout later
		// (when the user accepts privacy) does NOT re-create the navbar, leaving
		// the flyout unreachable for the rest of the session.
		// Privacy gating now happens at the *button* level inside StartHomePage
		// (Connect/SelectFile/Demo are disabled until accepted) and at Firebase
		// analytics opt-in, not at the Shell navigation level. Letting users tap
		// through to Settings / D-TAC before accepting is acceptable: D-TAC has
		// no committed selection so it shows nothing.
		FlyoutIcon = FlyoutIconImage;
		FlyoutBehavior = FlyoutBehavior.Flyout;

		this.BindingContext = easterEggPageViewModel;
		this.SetBinding(BackgroundColorProperty, static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor);
		this.SetBinding(TitleColorProperty, static (EasterEggPageViewModel vm) => vm.ShellTitleTextColor);

		FlyoutIconImage.BindingContext = easterEggPageViewModel;
		FlyoutIconImage.SetBinding(FontImageSource.ColorProperty, static (EasterEggPageViewModel vm) => vm.ShellTitleTextColor);

		// サーバーから HeaderColor コマンドを受信したときに、タイトルバー色を上書きする。
		// null (= ResetToDefault) の場合は EasterEgg の設定にフォールバックする。
		var appVm = InstanceManager.AppViewModel;
		appVm.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(AppViewModel.HeaderColorOverride_RGB))
				ApplyHeaderColorOverride(appVm.HeaderColorOverride_RGB, easterEggPageViewModel);
		};
		// 起動時の値も反映する (通常は null)
		ApplyHeaderColorOverride(appVm.HeaderColorOverride_RGB, easterEggPageViewModel);

		// FlyoutHeader / FlyoutFooter は AppViewModel を BindingContext として参照する。
		// Shell.BindingContext は EasterEggPageViewModel 用に予約されているため、
		// header/footer のサブツリーにのみローカルで割り当てる。
		DiagramInfoHeader.BindingContext = appVm;
		VersionFooter.BindingContext = appVm;

		// 行路に紐づく列車セクション (動的) を Privacy Policy の上に挿入する。
		// SelectedWork / OrderedTrainDataList の変化に応じて再構築する。
		_PrivacyPolicyShellItemRef = FindPrivacyPolicyShellItem();
		RebuildTrainMenuItems();
		appVm.PropertyChanged += OnAppViewModelPropertyChangedForTrainMenu;

		InstanceManager.AppViewModel.WindowWidth = DeviceDisplay.Current.MainDisplayInfo.Width;
		InstanceManager.AppViewModel.WindowHeight = DeviceDisplay.Current.MainDisplayInfo.Height;
		logger.Trace("Display Width/Height: {0}x{1}", InstanceManager.AppViewModel.WindowWidth, InstanceManager.AppViewModel.WindowHeight);

		DeviceDisplay.Current.MainDisplayInfoChanged += (s, e) =>
		{
			InstanceManager.AppViewModel.WindowWidth = e.DisplayInfo.Width;
			InstanceManager.AppViewModel.WindowHeight = e.DisplayInfo.Height;
			logger.Trace("Display Width/Height Changed: {0}x{1}", InstanceManager.AppViewModel.WindowWidth, InstanceManager.AppViewModel.WindowHeight);
#if IOS
			UpdateSafeAreaMargin();
#endif
		};

#if IOS
		UpdateSafeAreaMargin();
#endif

		logger.Trace("AppShell Created");
	}

	/// <summary>
	/// Flyout / MenuItem のタイトルを現在の言語で再設定する。"D-TAC" は
	/// ブランド名のため翻訳しない。
	/// </summary>
	void ApplyLocalization()
	{
		// Firebase/Privacy/TPL のサイドバー項目は main 側のリファクタ
		// (7ece849) で削除済みのため、現存する Home / Settings のみ再設定する。
		FlyoutStartHome.Title = AppResources.Shell_Home;
		FlyoutSettings.Title = AppResources.Shell_Settings;
		MenuPrivacyPolicyOnline.Text = AppResources.Shell_PrivacyPolicyOnline;
	}

	/// <summary>
	/// サーバー指示の色 (0xRRGGBB) でタイトルバーを上書きする。null の場合は
	/// EasterEgg ベースのバインディングを再有効化して端末設定に戻す。
	/// WebSocket 受信スレッドから呼ばれうるため、UI 操作は必ず MainThread に dispatch する。
	/// </summary>
	void ApplyHeaderColorOverride(int? rgbOrNull, EasterEggPageViewModel easterEggPageViewModel)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (rgbOrNull is int rgb)
			{
				byte r = (byte)((rgb >> 16) & 0xff);
				byte g = (byte)((rgb >> 8) & 0xff);
				byte b = (byte)(rgb & 0xff);
				this.RemoveBinding(BackgroundColorProperty);
				BackgroundColor = Color.FromRgb(r, g, b);
			}
			else
			{
				// バインディングを再設定して既定挙動に戻す
				this.SetBinding(BackgroundColorProperty, static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor);
			}
		});
	}

	/// <summary>
	/// XAML で宣言した Privacy Policy MenuItem を包む ShellItem を Items から探す。
	/// Shell は top-level &lt;MenuItem&gt; を内部の MenuShellItem (internal 型) に
	/// 自動ラップして Items に追加する。MenuShellItem は外部から参照不可だが、
	/// BaseShellItem.BindingContext 等から元 MenuItem を特定するのは脆い。
	/// 代わりに「初期状態の Items の末尾要素 = Privacy Policy の wrapper」と仮定して
	/// 起動時に直接掴む (XAML 上 Privacy Policy が最後の Shell 直下要素のため)。
	/// </summary>
	ShellItem? FindPrivacyPolicyShellItem()
	{
		// XAML 定義上、Privacy Policy MenuItem は Shell の最後の直接子要素なので
		// 初期化直後の Items でも最後の要素がそれにあたる。
		return Items.Count > 0 ? Items[Items.Count - 1] : null;
	}

	// 動的に挿入した列車セクション (見出し + 列車 MenuItem + separator) の参照。
	// 再構築時に Items から確実に取り除けるよう常にトラックする。
	readonly List<ShellItem> _DynamicTrainShellItems = new();
	ShellItem? _PrivacyPolicyShellItemRef;

	void OnAppViewModelPropertyChangedForTrainMenu(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppViewModel.SelectedWork)
			|| e.PropertyName == nameof(AppViewModel.OrderedTrainDataList))
		{
			MainThread.BeginInvokeOnMainThread(RebuildTrainMenuItems);
		}
	}

	/// <summary>
	/// Privacy Policy MenuItem の直前に「行路内 列車」セクション (見出し +
	/// 列車 1 個 1 MenuItem + 仕切り) を再構築する。
	/// プロトタイプ flyout.jsx L169-170 (sectionLabel('行路内 列車') + trainItem 列)
	/// に対応する。Shell の制約上、見出し / 仕切りは無効化 MenuItem で代用する。
	/// </summary>
	void RebuildTrainMenuItems()
	{
		// 既存の動的アイテムを除去
		foreach (var shellItem in _DynamicTrainShellItems)
		{
			Items.Remove(shellItem);
		}
		_DynamicTrainShellItems.Clear();

		var trains = InstanceManager.AppViewModel.OrderedTrainDataList;
		if (trains is null || trains.Count == 0)
			return;

		int insertIndex = _PrivacyPolicyShellItemRef is not null
			? Items.IndexOf(_PrivacyPolicyShellItemRef)
			: Items.Count;
		if (insertIndex < 0)
			insertIndex = Items.Count;

		void InsertMenuItem(MenuItem mi)
		{
			// ShellItem には MenuItem → ShellItem の implicit conversion が
			// 定義されており、暗黙ラッパーが生成される (MenuShellItem は internal で
			// 直接 new できないが、この変換を通せば同等のラップが得られる)。
			ShellItem wrapper = mi;
			Items.Insert(insertIndex, wrapper);
			_DynamicTrainShellItems.Add(wrapper);
			insertIndex++;
		}

		// セクション見出し (無効化 MenuItem で代用)
		InsertMenuItem(new MenuItem
		{
			Text = "—— 行路内 列車 ——",
			IsEnabled = false,
		});

		var currentSelected = InstanceManager.AppViewModel.SelectedTrainData;
		foreach (var train in trains)
		{
			string trainNumber = train.TrainNumber ?? string.Empty;
			string label = string.IsNullOrEmpty(train.Destination)
				? trainNumber
				: $"{trainNumber}  {train.Destination}";

			// 現在選択中の列車には先頭にマーカーを付ける (Bold 化は Shell.MenuItem では不可)。
			if (ReferenceEquals(train, currentSelected))
				label = "▶ " + label;

			TrainData captured = train;
			var mi = new MenuItem
			{
				Text = label,
				Command = new Command(() =>
				{
					InstanceManager.AppViewModel.SelectedTrainData = captured;
				}),
			};
			InsertMenuItem(mi);
		}

		// 仕切り (空 MenuItem)
		InsertMenuItem(new MenuItem
		{
			Text = "———————————————",
			IsEnabled = false,
		});
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

	public event ValueChangedEventHandler<Thickness>? SafeAreaMarginChanged;
	Thickness _SafeAreaMargin;
	public Thickness SafeAreaMargin
	{
		get => _SafeAreaMargin;
		private set
		{
			if (_SafeAreaMargin == value)
				return;

			static string FormatThickness(Thickness t)
			{
				return $"(Left:{t.Left}, Top:{t.Top}, Right:{t.Right}, Bottom:{t.Bottom})";
			}
			logger.Info("SafeAreaMargin Changed: {0} -> {1}", FormatThickness(_SafeAreaMargin), FormatThickness(value));
			Thickness tmp = _SafeAreaMargin;
			_SafeAreaMargin = value;
			SafeAreaMarginChanged?.Invoke(this, tmp, value);
		}
	}

#if IOS
	UIKit.UIWindow? UIWindow = null;

	[SupportedOSPlatform("ios13.0")]
	static UIKit.UIWindow? GetUIWindowOnIOS13OrLater()
	{
		if (UIKit.UIApplication.SharedApplication.ConnectedScenes.ToArray().FirstOrDefault(v => v is UIKit.UIWindowScene) is UIKit.UIWindowScene scene)
			return scene.Windows.FirstOrDefault();
		else
			return null;
	}

	[SupportedOSPlatform("ios")]
	[UnsupportedOSPlatform("ios15.0")]
	static UIKit.UIWindow? GetUIWindow()
	{
		return UIKit.UIApplication.SharedApplication.Windows.FirstOrDefault();
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		bool isIOS = OperatingSystem.IsIOS();
		logger.Info("OnSizeAllocated: {0}x{1} / ios:{2}", width, height, isIOS);
		if (!isIOS)
		{
			base.OnSizeAllocated(width, height);
			return;
		}

		UpdateSafeAreaMargin();
		base.OnSizeAllocated(width, height);
	}

	private void UpdateSafeAreaMargin()
	{
		// SafeAreaInsets ref: https://stackoverflow.com/questions/46829840/get-safe-area-inset-top-and-bottom-heights
		// ios15 >= ref: https://zenn.dev/paraches/articles/windows_was_depricated_in_ios15
		if (UIWindow is null)
		{
			UIWindow = OperatingSystem.IsIOSVersionAtLeast(13)
				? GetUIWindowOnIOS13OrLater()
				: GetUIWindow()
			;
			logger.Info("UIWindow: {0}", UIWindow is null ? "null" : UIWindow.ToString());
		}

		if (UIWindow is not null)
		{
			SafeAreaMargin = new(
				UIWindow.SafeAreaInsets.Left.Value,
				UIWindow.SafeAreaInsets.Top.Value,
				UIWindow.SafeAreaInsets.Right.Value,
				UIWindow.SafeAreaInsets.Bottom.Value
			);
		}
	}
#endif
}

