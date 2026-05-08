using OpenQA.Selenium;
using TRViS.UITests.Pages;

namespace TRViS.UITests.Tests;

/// <summary>
/// Verifies the "Load from Web" popup, including the connection-history
/// (URL history) list. Specifically guards against the reentrancy bug
/// where tapping a history row failed to populate the URL Entry.
/// </summary>
[TestFixture]
public class SelectOnlineResourcePopupTests : BaseUITest
{
	private static string SampleUrlA => StartHomePageObject.SeededHistoryUrls[0];
	private static string SampleUrlB => StartHomePageObject.SeededHistoryUrls[1];

	private StartHomePageObject _startHomePage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		_startHomePage = new StartHomePageObject(Driver);
		_startHomePage.AcceptPrivacyPolicyIfNeeded();
	}

	[Test]
	public void OpenPopup_ShowsAllExpectedControls()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);

		var popup = _startHomePage.OpenLoadFromWebPopup();

		Assert.That(popup.IsDisplayed(), Is.True, "Popup should be displayed.");
		Assert.That(popup.UrlInput.Displayed, Is.True);
		Assert.That(popup.UrlHistoryList.Displayed, Is.True);
		Assert.That(popup.LoadButton.Displayed, Is.True);
	}

	/// <summary>
	/// Reproduces and guards against the bug "現状接続履歴リストから項目を選択できない不具合":
	/// tapping a row in the URL history list must populate the URL Entry with that row's URL.
	/// </summary>
	// MAUI's CollectionView on WinUI 3 wraps each DataTemplate root in a
	// container that doesn't surface the inner Label in the UIA tree —
	// neither the Label's AutomationId nor its visible Name is reachable
	// via Appium. The other Windows lookups (TabButton, ToggleButton,
	// flyout items) all succeed via UIA, so this is specific to the
	// CollectionView item host. Skip on Windows pending a different
	// interaction strategy (e.g. scrolling the row into view explicitly,
	// or using mobile: dragFromToForDuration coords).
	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI CollectionView items don't surface in WinUI UIA tree.")]
	public void TapHistoryItem_PopulatesUrlInput()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);

		// Seed two URLs into history via the DEBUG-only hidden button (avoids
		// SendKeys, which is flaky on iOS XCUITest for long URLs).
		_startHomePage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		var popup = _startHomePage.OpenLoadFromWebPopup();

		// Tap the seeded URL. After the bug fix, the Entry should reflect this URL.
		popup.TapHistoryItem(SampleUrlA);

		// Allow the SelectionChanged -> Text setter -> handler chain to settle.
		Thread.Sleep(500);

		string entryText = popup.ReadUrlInputText();
		Assert.That(entryText, Is.EqualTo(SampleUrlA),
			$"Tapping history row '{SampleUrlA}' should set UrlInput.Text. Got: '{entryText}'");
	}

	[Test]
	[Platform(Exclude = "Win", Reason = "MAUI CollectionView items don't surface in WinUI UIA tree.")]
	public void TapDifferentHistoryItem_UpdatesUrlInput()
	{
		Assert.That(_startHomePage.IsDisplayed(), Is.True);

		_startHomePage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		var popup = _startHomePage.OpenLoadFromWebPopup();

		popup.TapHistoryItem(SampleUrlA);
		Thread.Sleep(300);
		Assert.That(popup.ReadUrlInputText(), Is.EqualTo(SampleUrlA));

		popup.TapHistoryItem(SampleUrlB);
		Thread.Sleep(300);
		Assert.That(popup.ReadUrlInputText(), Is.EqualTo(SampleUrlB),
			"Switching selection between history rows should update UrlInput.Text.");
	}

	[Test]
	public void Close_ReturnsToStartHomePage()
	{
		var popup = _startHomePage.OpenLoadFromWebPopup();
		Assert.That(popup.IsDisplayed(), Is.True);

		var back = popup.Close();
		Thread.Sleep(300);
		Assert.That(back.IsDisplayed(), Is.True, "After Close the StartHomePage should be visible again.");
	}
}
