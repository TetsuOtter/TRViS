using System.Reflection;

using TRViS.IO.Models.DB;
using TRViS.IO.RequestInfo;

namespace TRViS.IO.Tests;

public class OpenFileTests
{
	[OneTimeSetUp]
	public async Task SetUp()
	{
	}

	[Test]
	public void OnlyJsonPath()
	{
		AppLinkInfo actual = OpenFile.IdentifyAppLinkInfo("trvis:///app/open/json?path=https://example.com/db.json");
		AppLinkInfo expected = new(
			AppLinkInfo.FileType.Json,
			AppLinkInfo.CompressionType.None,
			AppLinkInfo.EncryptionType.None,
			new Uri("https://example.com/db.json"),
			null,
			null,
			null,
			null,
			null
		);
		Assert.That(actual, Is.EqualTo(expected));
	}

	[Test]
	public void OnlySqlitePath()
	{
		AppLinkInfo actual = OpenFile.IdentifyAppLinkInfo("trvis:///app/open/sqlite?path=https://example.com/trvis.db");
		AppLinkInfo expected = new(
			AppLinkInfo.FileType.Sqlite,
			AppLinkInfo.CompressionType.None,
			AppLinkInfo.EncryptionType.None,
			new Uri("https://example.com/trvis.db"),
			null,
			null,
			null,
			null,
			null
		);
		Assert.That(actual, Is.EqualTo(expected));
	}
}
