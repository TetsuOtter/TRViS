namespace TRViS.IO.RequestInfo.Tests;

public class AppLinkInfoTests
{
	static readonly Version expectedDefaultVersion = new(1, 0);
	const string EMPTY_JSON_BASE64 = "e30K";
	[Test]
	public void OnlyJsonPath()
	{
		AppLinkInfo actual = AppLinkInfo.FromAppLink("trvis://app/open/json?path=https://example.com/db.json");
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
		AppLinkInfo actual = AppLinkInfo.FromAppLink("trvis://app/open/sqlite?path=https://example.com/trvis.db");
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
				Assert.That(() => AppLinkInfo.FromAppLink(""), Throws.Exception.TypeOf<UriFormatException>());
				Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/"), Throws.ArgumentException);
				Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/"), Throws.ArgumentException);
			});

	[Test]
	public void UnknownFileType()
		=> Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/a"), Throws.ArgumentException);

	[Test]
	public void WithoutQuery()
		=> Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json"), Throws.ArgumentException);

	[Test]
	public void VersionTest()
	{
		Assert.That(
			AppLinkInfo.FromAppLink($"trvis://app/open/json?ver=&data={EMPTY_JSON_BASE64}").Version,
			Is.EqualTo(expectedDefaultVersion),
			"version empty => will be default"
		);
		Assert.That(
			AppLinkInfo.FromAppLink($"trvis://app/open/json?ver=0.1&data={EMPTY_JSON_BASE64}").Version,
			Is.EqualTo(new Version(0, 1)),
			"version 0.1"
		);
		Assert.That(
			() => AppLinkInfo.FromAppLink($"trvis://app/open/json?ver=2.0&data={EMPTY_JSON_BASE64}"),
			Throws.ArgumentException,
			"Unsupported version"
		);
	}

	[Test]
	public void WithoutPathAndData()
		=> Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?path="), Throws.ArgumentException);

	[Test]
	public void Path_WithoutScheme()
	{
		string appLink = "trvis://app/open/json?path=/abc/def";
		Uri? actual = AppLinkInfo.FromAppLink(appLink).ResourceUri;
		Assert.That(actual, Is.Not.Null);
		Assert.That(actual?.ToString(), Is.EqualTo("file:///abc/def"));
	}

	[Test]
	public void Local_BareFilename()
	{
		AppLinkInfo actual = AppLinkInfo.FromAppLink("trvis://app/open/json?local=foo.json");
		Assert.That(actual.LocalPath, Is.EqualTo("foo.json"));
		Assert.That(actual.ResourceUri, Is.Null);
		Assert.That(actual.FileTypeInfo, Is.EqualTo(AppLinkInfo.FileType.Json));
	}

	[Test]
	public void Local_NestedRelativePath()
	{
		AppLinkInfo actual = AppLinkInfo.FromAppLink("trvis://app/open/sqlite?local=sub/dir/db.sqlite");
		Assert.That(actual.LocalPath, Is.EqualTo("sub/dir/db.sqlite"));
		Assert.That(actual.FileTypeInfo, Is.EqualTo(AppLinkInfo.FileType.Sqlite));
	}

	[Test]
	public void Local_RejectsTraversal()
		=> Assert.Multiple(() =>
		{
			Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?local=../escape.json"),
				Throws.ArgumentException, "leading `..` segment");
			Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?local=sub/../escape.json"),
				Throws.ArgumentException, "embedded `..` segment");
			Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?local=sub/.."),
				Throws.ArgumentException, "trailing `..` segment");
		});

	[Test]
	public void Local_RejectsAbsoluteAndBackslash()
		=> Assert.Multiple(() =>
		{
			Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?local=/abs.json"),
				Throws.ArgumentException, "absolute path");
			Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?local=C:/win.json"),
				Throws.ArgumentException, "drive letter");
			Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?local=sub\\file.json"),
				Throws.ArgumentException, "backslash");
			Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?local=sub//file.json"),
				Throws.ArgumentException, "empty segment from doubled slash");
		});

	[Test]
	public void Local_RejectsCurrentDirSegment()
		=> Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?local=./file.json"),
			Throws.ArgumentException);

	[Test]
	public void NoPathDataOrLocal()
		=> Assert.That(() => AppLinkInfo.FromAppLink("trvis://app/open/json?ver=1.0"),
			Throws.ArgumentException);
}
