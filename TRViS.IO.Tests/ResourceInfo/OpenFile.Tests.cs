using System.Reflection;

using TRViS.IO.Models.DB;
using TRViS.IO.RequestInfo;

namespace TRViS.IO.Tests;

public class OpenFileTests
{
	static readonly Version expectedDefaultVersion = new(1, 0);
	const string EMPTY_JSON_BASE64 = "e30K";
	[Test]
	public void OnlyJsonPath()
	{
		AppLinkInfo actual = OpenFile.IdentifyAppLinkInfo("trvis://app/open/json?path=https://example.com/db.json");
		AppLinkInfo expected = new(
			AppLinkInfo.FileType.Json,
			expectedDefaultVersion,
			ResourceUri: new Uri("https://example.com/db.json")
		);
		Assert.That(actual, Is.EqualTo(expected));
	}

	[Test]
	public void OnlySqlitePath()
	{
		AppLinkInfo actual = OpenFile.IdentifyAppLinkInfo("trvis://app/open/sqlite?path=https://example.com/trvis.db");
		AppLinkInfo expected = new(
			AppLinkInfo.FileType.Sqlite,
			expectedDefaultVersion,
			ResourceUri: new Uri("https://example.com/trvis.db")
		);
		Assert.That(actual, Is.EqualTo(expected));
	}

	[Test]
	public void EmptyLink()
		=> Assert.Multiple(() =>
			{
				Assert.That(() => OpenFile.IdentifyAppLinkInfo(""), Throws.Exception.TypeOf<UriFormatException>());
				Assert.That(() => OpenFile.IdentifyAppLinkInfo("trvis://app/"), Throws.ArgumentException);
				Assert.That(() => OpenFile.IdentifyAppLinkInfo("trvis://app/open/"), Throws.ArgumentException);
			});

	[Test]
	public void UnknownFileType()
		=> Assert.That(() => OpenFile.IdentifyAppLinkInfo("trvis://app/open/a"), Throws.ArgumentException);

	[Test]
	public void WithoutQuery()
		=> Assert.That(() => OpenFile.IdentifyAppLinkInfo("trvis://app/open/json"), Throws.ArgumentException);

	[Test]
	public void VersionTest()
	{
		Assert.That(
			OpenFile.IdentifyAppLinkInfo($"trvis://app/open/json?ver=&data={EMPTY_JSON_BASE64}").Version,
			Is.EqualTo(expectedDefaultVersion),
			"version empty => will be default"
		);
		Assert.That(
			OpenFile.IdentifyAppLinkInfo($"trvis://app/open/json?ver=0.1&data={EMPTY_JSON_BASE64}").Version,
			Is.EqualTo(new Version(0, 1)),
			"version 0.1"
		);
		Assert.That(
			() => OpenFile.IdentifyAppLinkInfo($"trvis://app/open/json?ver=2.0&data={EMPTY_JSON_BASE64}"),
			Throws.ArgumentException,
			"Unsupported version"
		);
	}

	[Test]
	public void WithoutPathAndData()
		=> Assert.That(() => OpenFile.IdentifyAppLinkInfo("trvis://app/open/json?path="), Throws.ArgumentException);

	[Test]
	public void Path_WithoutScheme()
	{
		string appLink = "trvis://app/open/json?path=/abc/def";
		Uri? actual = OpenFile.IdentifyAppLinkInfo(appLink).ResourceUri;
		Assert.That(actual, Is.Not.Null);
		Assert.That(actual?.ToString(), Is.EqualTo("file:///abc/def"));
	}
}
