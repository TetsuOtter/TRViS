using NUnit.Framework;

using TRViS.NetworkSyncService.IntegrationTests.Helpers;

namespace TRViS.NetworkSyncService.IntegrationTests;

/// <summary>
/// NUnit の [SetUpFixture] を使ってテストアセンブリ全体で ServerFixture を共有する。
/// テスト間の状態は各テストの [SetUp] で POST /control/reset を呼び出してリセットする。
/// </summary>
[SetUpFixture]
public class GlobalServerSetup
{
	public static ServerFixture Server { get; private set; } = null!;

	[OneTimeSetUp]
	public async Task OneTimeSetUp()
	{
		Server = new ServerFixture();
		await Server.ControlClient.WaitForReadyAsync(timeout: TimeSpan.FromSeconds(30));
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		Server.Dispose();
	}
}
