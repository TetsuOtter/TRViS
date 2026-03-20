using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace TRViS.UITests.Infrastructure;

[TestFixture]
public abstract class BaseUITest
{
	protected AppiumDriver Driver { get; private set; } = null!;

	private static readonly TimeSpan DefaultImplicitWait = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan DefaultExplicitWait = TimeSpan.FromSeconds(30);

	[SetUp]
	public virtual void SetUp()
	{
		var platformStr = TestContext.Parameters["platform"]
			?? throw new InvalidOperationException("TestRunParameter 'platform' is required.");
		var appPath = TestContext.Parameters["appPath"]
			?? throw new InvalidOperationException("TestRunParameter 'appPath' is required.");
		var appiumUrl = TestContext.Parameters["appiumUrl"] ?? "http://localhost:4723";

		var platform = AppiumConfig.ParsePlatform(platformStr);
		var options = AppiumConfig.CreateOptions(platform, appPath);
		var serverUri = new Uri(appiumUrl);

		Driver = platform switch
		{
			TestPlatform.Android => new AndroidDriver(serverUri, options),
			TestPlatform.iOS => new IOSDriver(serverUri, options),
			TestPlatform.MacCatalyst => new MacDriver(serverUri, options),
			TestPlatform.Windows => new WindowsDriver(serverUri, options),
			_ => throw new ArgumentOutOfRangeException(nameof(platform)),
		};

		Driver.Manage().Timeouts().ImplicitWait = DefaultImplicitWait;
	}

	[TearDown]
	public virtual void TearDown()
	{
		if (Driver is not null)
		{
			TakeScreenshot();
			Driver.Quit();
		}
	}

	protected AppiumElement FindByAutomationId(string automationId)
		=> Driver.FindElement(MobileBy.AccessibilityId(automationId));

	protected AppiumElement WaitForElement(string automationId, TimeSpan? timeout = null)
	{
		var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(
			Driver,
			timeout ?? DefaultExplicitWait);

		return (AppiumElement)wait.Until(d =>
		{
			try
			{
				var element = d.FindElement(MobileBy.AccessibilityId(automationId));
				return element.Displayed ? element : null!;
			}
			catch (NoSuchElementException)
			{
				return null!;
			}
		});
	}

	protected void TakeScreenshot()
	{
		try
		{
			var screenshot = Driver.GetScreenshot();
			var testName = TestContext.CurrentContext.Test.FullName
				.Replace(' ', '_')
				.Replace('/', '_');
			var path = Path.Combine(
				TestContext.CurrentContext.WorkDirectory,
				$"{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
			screenshot.SaveAsFile(path);
			TestContext.AddTestAttachment(path, "Screenshot");
		}
		catch (Exception ex)
		{
			TestContext.Out.WriteLine($"Screenshot failed: {ex.Message}");
		}
	}
}
