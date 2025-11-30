using Foundation;
using UIKit;
using ObjCRuntime;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace TRViS;

/// <summary>
/// NavigationPage のバックボタンアイコンをMaterialIconRegularフォントでカスタマイズするための実装
/// </summary>
public static class NavigationPageCustomizationSetup
{
	private static bool _isInitialized = false;

	public static void SetupNavigationPageHandler()
	{
		if (_isInitialized)
			return;

		_isInitialized = true;

		try
		{
			// NavigationBar のグローバル外観をカスタマイズ
			CustomizeNavigationBarGlobalAppearance();

			// MAUI の NavigationPage ハンドラーをカスタマイズ
			HookMauiNavigation();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error setting up NavigationPage customization: {ex.Message}");
		}
	}

	private static void HookMauiNavigation()
	{
		try
		{
			// MainThread で MAUI のナビゲーション初期化後にセットアップ
			MainThread.BeginInvokeOnMainThread(() =>
			{
				// ウィンドウが準備できた後に NavigationPage をフック
				var app = Application.Current;
				if (app != null)
				{
					CustomizeCurrentNavigationController();
				}
			});
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error hooking MAUI navigation: {ex.Message}");
		}
	}

	private static void CustomizeCurrentNavigationController()
	{
		try
		{
			// iOS 13+ でのみ ConnectedScenes を使用
			if (OperatingSystem.IsIOSVersionAtLeast(13))
			{
#pragma warning disable CA1416
				var window = UIApplication.SharedApplication.ConnectedScenes
					.OfType<UIWindowScene>()
					.FirstOrDefault()
					?.Windows
					.FirstOrDefault();

				if (window?.RootViewController is UINavigationController navController)
				{
					ApplyNavigationBarCustomization(navController);
				}
#pragma warning restore CA1416
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error customizing current navigation controller: {ex.Message}");
		}
	}

	private static void ApplyNavigationBarCustomization(UINavigationController navController)
	{
		try
		{
			// NavigationBar をカスタマイズ
			var font = UIFont.FromName("MaterialIconsRegular", 18);
			if (font == null)
			{
				System.Diagnostics.Debug.WriteLine("MaterialIconsRegular font not found");
				return;
			}

			// バーボタンアイテムのフォントをカスタマイズ
			var attributes = new UIStringAttributes { Font = font };
			UIBarButtonItem.Appearance.SetTitleTextAttributes(attributes, UIControlState.Normal);

			// すべての表示されている ViewControllers のバックボタンをカスタマイズ
			if (navController.ViewControllers != null)
			{
				foreach (var vc in navController.ViewControllers)
				{
					if (navController.ViewControllers.Length > 1)
					{
						// バックボタンを作成 (Material Icons)
						var backButton = new UIBarButtonItem(
							title: "\ue5c4", // Material Icons: arrow_back
							style: UIBarButtonItemStyle.Plain,
							target: null,
							action: null);
						backButton.SetTitleTextAttributes(attributes, UIControlState.Normal);
						vc.NavigationItem.BackBarButtonItem = backButton;
					}
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error applying NavigationBar customization: {ex.Message}");
		}
	}

	/// <summary>
	/// アプリ全体の NavigationBar 外観を設定
	/// </summary>
	private static void CustomizeNavigationBarGlobalAppearance()
	{
		try
		{
			var font = UIFont.FromName("MaterialIconsRegular", 18);
			if (font == null)
			{
				System.Diagnostics.Debug.WriteLine("MaterialIconsRegular font not loaded");
				return;
			}

			// NavigationBar のバーボタンアイテムのフォントをMaterialIconsRegularに設定
			var attributes = new UIStringAttributes { Font = font };

			// iOS 5 以上でサポートされている方法
			UIBarButtonItem.Appearance.SetTitleTextAttributes(attributes, UIControlState.Normal);
			UIBarButtonItem.Appearance.SetTitleTextAttributes(attributes, UIControlState.Highlighted);
			UIBarButtonItem.Appearance.SetTitleTextAttributes(attributes, UIControlState.Disabled);

			// iOS 13+ でのより詳細な外観カスタマイズ
			if (OperatingSystem.IsIOSVersionAtLeast(13))
			{
				CustomizeNavigationBarAppearanceIOS13Plus();
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error customizing NavigationBar global appearance: {ex.Message}");
		}
	}

	/// <summary>
	/// iOS 13+ 専用の UINavigationBarAppearance を使用したカスタマイズ
	/// </summary>
	private static void CustomizeNavigationBarAppearanceIOS13Plus()
	{
		if (!OperatingSystem.IsIOSVersionAtLeast(13))
			return;

		try
		{
#pragma warning disable CA1416
			// バーボタンアイテムのフォントをMaterialIconsRegularに設定するための辞書を作成
			var fontDict = (NSDictionary<NSString, NSObject>)(object)new UIStringAttributes
			{
				Font = UIFont.FromName("MaterialIconsRegular", 18)
			}.Dictionary;

			// UINavigationBar の外観を設定
			var navigationBarAppearance = new UINavigationBarAppearance();

			// バーボタンアイテムのフォントをMaterialIconsRegularに設定
			var barButtonItemAppearance = new UIBarButtonItemAppearance();
			barButtonItemAppearance.Normal.TitleTextAttributes = fontDict;
			barButtonItemAppearance.Highlighted.TitleTextAttributes = fontDict;
			barButtonItemAppearance.Disabled.TitleTextAttributes = fontDict;
			barButtonItemAppearance.Focused.TitleTextAttributes = fontDict;

			navigationBarAppearance.ButtonAppearance = barButtonItemAppearance;
			navigationBarAppearance.BackButtonAppearance = barButtonItemAppearance;

			// NavigationBar に適用
			UINavigationBar.Appearance.StandardAppearance = navigationBarAppearance;

			if (OperatingSystem.IsIOSVersionAtLeast(15))
			{
				UINavigationBar.Appearance.ScrollEdgeAppearance = navigationBarAppearance;
				UINavigationBar.Appearance.CompactScrollEdgeAppearance = navigationBarAppearance;
			}
#pragma warning restore CA1416
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error customizing NavigationBar appearance iOS 13+: {ex.Message}");
		}
	}
}
