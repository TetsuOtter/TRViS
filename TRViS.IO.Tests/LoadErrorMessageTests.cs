using System.Net;
using System.Net.Http;
using System.Text.Json;

using SQLite;

namespace TRViS.IO.Tests;

// LoadErrorMessage.Describe only classifies the exception; the user-facing
// strings are produced (and localized) in the app layer. These tests assert
// the language-independent classification + carried parameters, not any
// Japanese/English wording (issue #40).
public class LoadErrorMessageTests
{
	[Test]
	public void MalformedJson_ClassifiesAsJsonMalformedWithPosition()
	{
		JsonException ex = Assert.Throws<JsonException>(
			() => JsonSerializer.Deserialize<int[]>("{ broken"))!;

		LoadErrorInfo info = LoadErrorMessage.Describe(ex);

		Assert.Multiple(() =>
		{
			Assert.That(info.Kind, Is.EqualTo(LoadErrorKind.JsonMalformed));
			// A real parse failure carries a line number, so the position
			// fields must be populated (1-based).
			Assert.That(info.JsonLine, Is.Not.Null);
			Assert.That(info.JsonLine, Is.GreaterThanOrEqualTo(1));
			Assert.That(info.JsonColumn, Is.Not.Null);
			// The raw library text must NOT be carried for a recognised type.
			Assert.That(info.RawDetail, Is.Null);
		});
	}

	[Test]
	public void JsonException_WithoutPosition_OmitsPositionFields()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new JsonException("raw"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Kind, Is.EqualTo(LoadErrorKind.JsonMalformed));
			Assert.That(info.JsonLine, Is.Null);
			Assert.That(info.JsonColumn, Is.Null);
			Assert.That(info.RawDetail, Is.Null);
		});
	}

	[TestCase(SQLite3.Result.NonDBFile, LoadErrorKind.SqliteCorrupt)]
	[TestCase(SQLite3.Result.Corrupt, LoadErrorKind.SqliteCorrupt)]
	[TestCase(SQLite3.Result.Empty, LoadErrorKind.SqliteCorrupt)]
	[TestCase(SQLite3.Result.Format, LoadErrorKind.SqliteCorrupt)]
	[TestCase(SQLite3.Result.CannotOpen, LoadErrorKind.SqliteCannotOpen)]
	[TestCase(SQLite3.Result.Perm, LoadErrorKind.SqlitePermission)]
	[TestCase(SQLite3.Result.AccessDenied, LoadErrorKind.SqlitePermission)]
	[TestCase(SQLite3.Result.ReadOnly, LoadErrorKind.SqlitePermission)]
	[TestCase(SQLite3.Result.IOError, LoadErrorKind.SqliteIO)]
	[TestCase(SQLite3.Result.Busy, LoadErrorKind.SqliteBusy)]
	[TestCase(SQLite3.Result.Locked, LoadErrorKind.SqliteBusy)]
	[TestCase(SQLite3.Result.Constraint, LoadErrorKind.SqliteOther)]
	public void SqliteException_MapsResultToKind(SQLite3.Result result, LoadErrorKind expected)
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(SQLiteException.New(result, "near \"x\": syntax error"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Kind, Is.EqualTo(expected));
			// The raw library text must NOT be carried for a recognised type.
			Assert.That(info.RawDetail, Is.Null);
		});
	}

	[Test]
	public void EmptyJson_DeserializesToNull_ClassifiesAsEmptyOrNull()
	{
		// LoaderJson throws ArgumentNullException when the JSON is the
		// literal `null` (or an empty stream) — the "empty file" case.
		ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
			() => LoaderJson.InitFromBytes("null"u8))!;

		LoadErrorInfo info = LoadErrorMessage.Describe(ex);

		Assert.That(info.Kind, Is.EqualTo(LoadErrorKind.EmptyOrNull));
	}

	[Test]
	public void FileNotFound_TakesPrecedenceOverGenericIO()
	{
		Assert.Multiple(() =>
		{
			Assert.That(
				LoadErrorMessage.Describe(new FileNotFoundException()).Kind,
				Is.EqualTo(LoadErrorKind.FileNotFound));
			Assert.That(
				LoadErrorMessage.Describe(new DirectoryNotFoundException()).Kind,
				Is.EqualTo(LoadErrorKind.FileNotFound));
		});
	}

	[Test]
	public void GenericIOException_ClassifiesAsIOError_WithoutRawDetail()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new IOException("disk gone"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Kind, Is.EqualTo(LoadErrorKind.IOError));
			Assert.That(info.RawDetail, Is.Null);
		});
	}

	[Test]
	public void UnauthorizedAccess_ClassifiesAsUnauthorized()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new UnauthorizedAccessException());

		Assert.That(info.Kind, Is.EqualTo(LoadErrorKind.Unauthorized));
	}

	[Test]
	public void HttpRequestException_WithStatusCode_CarriesCode()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(
			new HttpRequestException("boom", null, HttpStatusCode.NotFound));

		Assert.Multiple(() =>
		{
			Assert.That(info.Kind, Is.EqualTo(LoadErrorKind.HttpWithStatus));
			Assert.That(info.HttpStatusCode, Is.EqualTo(404));
			Assert.That(info.HttpStatusName, Is.EqualTo(nameof(HttpStatusCode.NotFound)));
			// The raw library text must NOT be carried for a recognised type.
			Assert.That(info.RawDetail, Is.Null);
		});
	}

	[Test]
	public void HttpRequestException_WithoutStatusCode_ClassifiesAsHttpNoStatus()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(new HttpRequestException("boom"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Kind, Is.EqualTo(LoadErrorKind.HttpNoStatus));
			Assert.That(info.HttpStatusCode, Is.Null);
		});
	}

	[Test]
	public void Timeout_ClassifiesAsTimeout_BareAndWrapped()
	{
		LoadErrorInfo bare = LoadErrorMessage.Describe(new TimeoutException());
		LoadErrorInfo wrapped = LoadErrorMessage.Describe(
			new TaskCanceledException("timed out", new TimeoutException()));

		Assert.Multiple(() =>
		{
			Assert.That(bare.Kind, Is.EqualTo(LoadErrorKind.Timeout));
			Assert.That(wrapped.Kind, Is.EqualTo(LoadErrorKind.Timeout));
		});
	}

	[Test]
	public void UnknownException_FallsBackAndCarriesRawDetail()
	{
		LoadErrorInfo info = LoadErrorMessage.Describe(
			new InvalidOperationException("something obscure"));

		Assert.Multiple(() =>
		{
			Assert.That(info.Kind, Is.EqualTo(LoadErrorKind.Unknown));
			// Only the unrecognised path is allowed to carry raw text.
			Assert.That(info.RawDetail, Is.EqualTo("something obscure"));
		});
	}
}
