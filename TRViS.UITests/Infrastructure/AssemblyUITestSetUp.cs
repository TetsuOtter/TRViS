// Assembly-wide [SetUpFixture]: NUnit applies a SetUpFixture to every
// test under its declared namespace (and sub-namespaces). Placing this
// type in the root TRViS.UITests namespace makes it cover both
// TRViS.UITests.Tests.* and TRViS.UITests.Infrastructure.* — the whole
// suite.

namespace TRViS.UITests;

using TRViS.UITests.Infrastructure;

/// <summary>
/// Assembly-scoped lifecycle for the UI-test suite: creates the Appium
/// session once at the start of the run and tears it down once at the end,
/// so the app process is reused across every fixture. Without this, each
/// <see cref="BaseUITest"/>-derived fixture's OneTimeSetUp would
/// ResetAppState + create its own session — paying the session-attach
/// cost (~17 s on mac2, ~10-15 s on XCUITest, ~5 s on UiAutomator2) for
/// every fixture boundary and visibly killing/relaunching the app between
/// fixtures even though within each fixture the session is already shared.
///
/// Each fixture's own [SetUp] recovers state via in-app test seams
/// (NavigateToHome, ClearLoaderForTesting, ClearSampleFilesForTesting,
/// dialog Close, AcceptPrivacyPolicyIfNeeded fast-path, etc.) rather than
/// relying on a fresh process. The two fixtures that depend on
/// "privacy-banner visible on fresh install" — AppLaunchTests and
/// FirebaseSettingTests — use [Order(1)] / [Order(2)] so they run first,
/// before any other fixture can accept the banner.
/// </summary>
[SetUpFixture]
public class AssemblyUITestSetUp
{
	[OneTimeSetUp]
	public void GlobalSetUp()
	{
		BaseUITest.InitGlobalSession();
	}

	[OneTimeTearDown]
	public void GlobalTearDown()
	{
		BaseUITest.QuitGlobalSession();
	}
}
