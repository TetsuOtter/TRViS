using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.iOS;
using System;
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Appium;
using System.Threading.Tasks;

namespace TRViS.UITests
{
	[TestFixture]
	public class DTACPageTests
	{
		private IOSDriver driver;

		[SetUp]
		public void SetUp()
		{
			var options = new AppiumOptions();
			options.PlatformName = "iOS";
			string displayName = Environment.GetEnvironmentVariable("UITEST_DEVICE") ?? "iPhone 16";
			string platformVersion = Environment.GetEnvironmentVariable("UITEST_PLATFORM_VERSION") ?? "17.5";

			// デバイス名→内部ID変換キャッシュ
			string cachePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "simctl_devicetype_cache.txt");
			string deviceTypeId = null;
			if (File.Exists(cachePath))
			{
				foreach (var line in File.ReadAllLines(cachePath))
				{
					var parts = line.Split('|');
					if (parts.Length == 2 && parts[0] == displayName)
					{
						deviceTypeId = parts[1];
						break;
					}
				}
			}
			if (deviceTypeId == null)
			{
				// deviceName, deviceTypeId, platformVersion を uitest_devices.txt から取得
				string[] deviceInfo = displayName.Split('|');
				displayName = deviceInfo[0];
				deviceTypeId = deviceInfo.Length > 1 ? deviceInfo[1] : null;
				platformVersion = deviceInfo.Length > 2 ? deviceInfo[2] : null;

				// deviceTypeId, platformVersionが必須
				if (string.IsNullOrEmpty(deviceTypeId) || string.IsNullOrEmpty(platformVersion))
				{
					throw new Exception($"Invalid device info: {displayName}");
				}
			}

			// Debug: Desired capabilities
			Console.WriteLine($"[AppiumOptions] DeviceName: {displayName}");
			Console.WriteLine($"[AppiumOptions] deviceTypeId: {deviceTypeId}");
			Console.WriteLine($"[AppiumOptions] PlatformVersion: {platformVersion}");
			Console.WriteLine($"[AppiumOptions] App: /Users/tetsu/Projects/TRViS/TRViS/bin/Debug/net10.0-ios/iossimulator-arm64/TRViS.app");

			options.DeviceName = displayName;
			options.AddAdditionalAppiumOption("deviceType", deviceTypeId);
			options.PlatformVersion = platformVersion;
			options.App = "/Users/tetsu/Projects/TRViS/TRViS/bin/Debug/net10.0-ios/iossimulator-arm64/TRViS.app";
			options.AutomationName = "XCUITest";

			// Appium connection retry logic
			int maxRetries = 3;
			int retryCount = 0;
			int waitSeconds = 60;
			Exception lastException = null;
			while (retryCount < maxRetries)
			{
				try
				{
					DateTime start = DateTime.Now;
					while (true)
					{
						try
						{
							driver = new IOSDriver(new Uri("http://127.0.0.1:4723/"), options);
							break;
						}
						catch (OpenQA.Selenium.WebDriverException ex)
						{
							if ((DateTime.Now - start).TotalSeconds > waitSeconds)
							{
								throw;
							}
							System.Threading.Thread.Sleep(2000); // 2秒待機してリトライ
						}
					}
					// 成功したら抜ける
					return;
				}
				catch (Exception ex)
				{
					lastException = ex;
					Console.WriteLine($"Appium connection failed (try {retryCount + 1}/{maxRetries}): {ex.Message}");
					System.Threading.Thread.Sleep(5000); // 5秒待機してリトライ
					retryCount++;
				}
			}
			throw new Exception($"Appium connection failed after {maxRetries} retries.", lastException);
		}

		[TearDown]
		public void TearDown()
		{
			driver?.Quit();
		}

		[Test]
		public void OpenDTACPageAndTakeScreenshot()
		{
			// テスト開始直後の画面を保存
			var initialScreenshot = driver.GetScreenshot();
			var initialScreenshotPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "initial_screenshot.png");
			initialScreenshot.SaveAsFile(initialScreenshotPath);
			TestContext.AddTestAttachment(initialScreenshotPath, "Initial Page Screenshot");
			Console.WriteLine($"Initial screenshot saved: {initialScreenshotPath}");

			// メニューを開く（最大10秒待機）
			var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(driver, TimeSpan.FromSeconds(10));
			var menuButton = wait.Until(drv => drv.FindElement(OpenQA.Selenium.Appium.MobileBy.AccessibilityId("MenuButton")));
			((IWebElement)menuButton).Click();

			// D-TACページを選択
			var dtacMenuItem = driver.FindElement(OpenQA.Selenium.Appium.MobileBy.AccessibilityId("MenuItem_DTAC"));
			((IWebElement)dtacMenuItem).Click();

			// D-TACページが表示されるまで待機
			var dtacPage = driver.FindElement(OpenQA.Selenium.Appium.MobileBy.AccessibilityId("DTACPage"));
			Assert.IsNotNull(dtacPage);
			System.Threading.Thread.Sleep(2000);

			// 「ハコ」タブのスクリーンショット取得
			var screenshotHako = driver.GetScreenshot();
			var screenshotHakoPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dtac_hako_tab.png");
			screenshotHako.SaveAsFile(screenshotHakoPath);
			TestContext.AddTestAttachment(screenshotHakoPath, "D-TAC Page ハコタブ Screenshot");
			Console.WriteLine($"D-TAC ハコタブ screenshot saved: {screenshotHakoPath}");

			// 「時刻表」タブに切り替え
			var timetableTab = driver.FindElement(OpenQA.Selenium.Appium.MobileBy.AccessibilityId("VerticalViewTabButton"));
			((IWebElement)timetableTab).Click();

			// 「時刻表」タブのスクリーンショット取得
			System.Threading.Thread.Sleep(5000);
			var screenshotTimetable = driver.GetScreenshot();
			var screenshotTimetablePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dtac_timetable_tab.png");
			screenshotTimetable.SaveAsFile(screenshotTimetablePath);
			TestContext.AddTestAttachment(screenshotTimetablePath, "D-TAC Page 時刻表タブ Screenshot");
			Console.WriteLine($"D-TAC 時刻表タブ screenshot saved: {screenshotTimetablePath}");
		}
	}
}
