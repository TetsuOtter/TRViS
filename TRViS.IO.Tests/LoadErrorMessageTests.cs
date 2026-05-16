using System.Net;
using System.Net.Http;
using System.Text.Json;

using SQLite;

namespace TRViS.IO.Tests;

public class LoadErrorMessageTests
{
	const string FileErrorTitle = "読み込めませんでした";

	[Test]
	public void MalformedJson_GivesFriendlyBodyWithPosition()
	{
		JsonException ex = Assert.Throws<JsonException>(
			() => JsonSerializer.Deserialize<int[]>("{ broken"))!;

		LoadErrorInfo info = LoadErrorMessage.Describe(ex);

		Assert.Multiple(() =>
		{
			Assert.That(info.Title, Is.EqualTo(FileErrorTitle));
			Assert.That(info.Body, Does.Contain("JSON"));
			// A real parse failure carries a line number, so the position
			// hint must be appended.
			Assert.That(info.Body, Does.Contain("行目"));
			// The raw library text must NOT be surfaced for a recognised type.
			Assert.That(info.Body, Does.Not.Contain("詳細:"));
		});
	}

	[Test]
	public void JsonException_WithoutPosition_OmitsPositionHint()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new JsonException("raw"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Body, Does.Contain("JSON"));
			Assert.That(info.Body, Does.Not.Contain("行目"));
			Assert.That(info.Body, Does.Not.Contain("raw"));
		});
	}

	[TestCase(SQLite3.Result.NonDBFile, "対応した形式")]
	[TestCase(SQLite3.Result.Corrupt, "対応した形式")]
	[TestCase(SQLite3.Result.CannotOpen, "開けませんでした")]
	[TestCase(SQLite3.Result.Perm, "アクセスが拒否")]
	[TestCase(SQLite3.Result.AccessDenied, "アクセスが拒否")]
	[TestCase(SQLite3.Result.IOError, "読み込み中にエラー")]
	[TestCase(SQLite3.Result.Busy, "使用中")]
	[TestCase(SQLite3.Result.Locked, "使用中")]
	[TestCase(SQLite3.Result.Constraint, "失敗しました")]
	public void SqliteException_MapsResultToFriendlyBody(SQLite3.Result result, string expectedFragment)
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(SQLiteException.New(result, "near \"x\": syntax error"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Title, Is.EqualTo(FileErrorTitle));
			Assert.That(info.Body, Does.Contain(expectedFragment));
			Assert.That(info.Body, Does.Not.Contain("near \"x\""));
		});
	}

	[Test]
	public void EmptyJson_DeserializesToNull_GivesEmptyFileMessage()
	{
		// LoaderJson throws ArgumentNullException when the JSON is the
		// literal `null` (or an empty stream) — this is the "empty file"
		// user-facing case.
		ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
			() => LoaderJson.InitFromBytes("null"u8))!;

		LoadErrorInfo info = LoadErrorMessage.Describe(ex);

		Assert.That(info.Body, Does.Contain("空"));
	}

	[Test]
	public void FileNotFound_TakesPrecedenceOverGenericIO()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new FileNotFoundException());

		Assert.That(info.Body, Does.Contain("見つかりません"));
	}

	[Test]
	public void GenericIOException_GivesReadErrorMessage()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new IOException("disk gone"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Body, Does.Contain("読み込み中にエラー"));
			Assert.That(info.Body, Does.Not.Contain("disk gone"));
		});
	}

	[Test]
	public void UnauthorizedAccess_GivesPermissionMessage()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new UnauthorizedAccessException());

		Assert.That(info.Body, Does.Contain("アクセスが拒否"));
	}

	[Test]
	public void HttpRequestException_WithStatusCode_ShowsCode()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(
			new HttpRequestException("boom", null, HttpStatusCode.NotFound));

		Assert.Multiple(() =>
		{
			Assert.That(info.Title, Is.EqualTo("接続できませんでした"));
			Assert.That(info.Body, Does.Contain("404"));
			Assert.That(info.Body, Does.Not.Contain("boom"));
		});
	}

	[Test]
	public void HttpRequestException_WithoutStatusCode_AsksToCheckNetwork()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new HttpRequestException("boom"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Title, Is.EqualTo("接続できませんでした"));
			Assert.That(info.Body, Does.Contain("ネットワーク接続"));
		});
	}

	[Test]
	public void Timeout_GivesNetworkGuidance()
	{
		LoadErrorInfo bare = LoadErrorMessage.Describe(new TimeoutException());
		LoadErrorInfo wrapped = LoadErrorMessage.Describe(
			new TaskCanceledException("timed out", new TimeoutException()));

		Assert.Multiple(() =>
		{
			Assert.That(bare.Title, Is.EqualTo("接続できませんでした (Timeout)"));
			Assert.That(bare.Body, Does.Contain("ファイアウォール"));
			Assert.That(wrapped.Title, Is.EqualTo("接続できませんでした (Timeout)"));
			Assert.That(wrapped.Body, Does.Contain("ファイアウォール"));
		});
	}

	[Test]
	public void UnknownException_FallsBackAndIncludesRawDetail()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(
			new InvalidOperationException("something obscure"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Title, Is.EqualTo(FileErrorTitle));
			// Only the unrecognised path is allowed to surface raw text.
			Assert.That(info.Body, Does.Contain("詳細:"));
			Assert.That(info.Body, Does.Contain("something obscure"));
		});
	}
}
