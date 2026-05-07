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
	private static string SampleUrlA => SelectTrainPageObject.SeededHistoryUrls[0];
	private static string SampleUrlB => SelectTrainPageObject.SeededHistoryUrls[1];

	private SelectTrainPageObject _selectTrainPage = null!;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		var firebasePage = new FirebaseSettingPageObject(Driver);
		// Use the page object's platform-aware default timeout (120 s on Android,
		// 15 s on Windows where MAUI Preferences may not have been reset).
		if (firebasePage.IsDisplayed())
			_selectTrainPage = firebasePage.SaveAndAccept();
		else
			_selectTrainPage = new SelectTrainPageObject(Driver);
	}

	[Test]
	public void OpenPopup_ShowsAllExpectedControls()
	{
		Assert.That(_selectTrainPage.IsDisplayed(), Is.True);

		var popup = _selectTrainPage.OpenLoadFromWebPopup();

		Assert.That(popup.IsDisplayed(), Is.True, "Popup should be displayed.");
		Assert.That(popup.UrlInput.Displayed, Is.True);
		Assert.That(popup.UrlHistoryList.Displayed, Is.True);
		Assert.That(popup.LoadButton.Displayed, Is.True);
	}

	/// <summary>
	/// Reproduces and guards against the bug "現状接続履歴リストから項目を選択できない不具合":
	/// tapping a row in the URL history list must populate the URL Entry with that row's URL.
	/// </summary>
	[Test]
	public void TapHistoryItem_PopulatesUrlInput()
	{
		Assert.That(_selectTrainPage.IsDisplayed(), Is.True);

		// Seed two URLs into history via the DEBUG-only hidden button (avoids
		// SendKeys, which is flaky on iOS XCUITest for long URLs).
		_selectTrainPage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		var popup = _selectTrainPage.OpenLoadFromWebPopup();

		// Tap the seeded URL. After the bug fix, the Entry should reflect this URL.
		popup.TapHistoryItem(SampleUrlA);

		// Allow the SelectionChanged -> Text setter -> handler chain to settle.
		Thread.Sleep(500);

		string entryText = popup.ReadUrlInputText();
		Assert.That(entryText, Is.EqualTo(SampleUrlA),
			$"Tapping history row '{SampleUrlA}' should set UrlInput.Text. Got: '{entryText}'");
	}

	[Test]
	public void TapDifferentHistoryItem_UpdatesUrlInput()
	{
		Assert.That(_selectTrainPage.IsDisplayed(), Is.True);

		_selectTrainPage.SeedUrlHistoryForTesting();
		Thread.Sleep(300);

		var popup = _selectTrainPage.OpenLoadFromWebPopup();

		popup.TapHistoryItem(SampleUrlA);
		Thread.Sleep(300);
		Assert.That(popup.ReadUrlInputText(), Is.EqualTo(SampleUrlA));

		popup.TapHistoryItem(SampleUrlB);
		Thread.Sleep(300);
		Assert.That(popup.ReadUrlInputText(), Is.EqualTo(SampleUrlB),
			"Switching selection between history rows should update UrlInput.Text.");
	}

	[Test]
	public void Close_ReturnsToSelectTrainPage()
	{
		var popup = _selectTrainPage.OpenLoadFromWebPopup();
		Assert.That(popup.IsDisplayed(), Is.True);

		var back = popup.Close();
		Thread.Sleep(300);
		Assert.That(back.IsDisplayed(), Is.True, "After Close the SelectTrainPage should be visible again.");
	}
}
