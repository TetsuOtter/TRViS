using TRViS.DTAC.Logic.Formatters;
using TRViS.IO.Models;

using Xunit;

namespace TRViS.DTAC.Logic.Tests;

public class HorizontalTimetableContentBuilderTests
{
	private static Work MakeWork(
		bool? hasETrain = null,
		int? contentType = null,
		byte[]? content = null)
		=> new Work(
			Id: "w1",
			WorkGroupId: "wg1",
			Name: "test",
			HasETrainTimetable: hasETrain,
			ETrainTimetableContentType: contentType,
			ETrainTimetableContent: content);

	#region HasHorizontalTimetable

	[Fact]
	public void HasHorizontalTimetable_NullWork_ReturnsFalse()
		=> Assert.False(HorizontalTimetableContentBuilder.HasHorizontalTimetable(null));

	[Fact]
	public void HasHorizontalTimetable_FlagFalse_ReturnsFalse()
		=> Assert.False(HorizontalTimetableContentBuilder.HasHorizontalTimetable(
			MakeWork(hasETrain: false, content: new byte[] { 1 })));

	[Fact]
	public void HasHorizontalTimetable_NullContent_ReturnsFalse()
		=> Assert.False(HorizontalTimetableContentBuilder.HasHorizontalTimetable(
			MakeWork(hasETrain: true, content: null)));

	[Fact]
	public void HasHorizontalTimetable_FlagTrueAndContentPresent_ReturnsTrue()
		=> Assert.True(HorizontalTimetableContentBuilder.HasHorizontalTimetable(
			MakeWork(hasETrain: true, content: new byte[] { 0xff })));

	#endregion

	#region Build — empty / no content

	[Fact]
	public void Build_NullWork_ReturnsNone()
	{
		var result = HorizontalTimetableContentBuilder.Build(null);
		Assert.Equal(HorizontalTimetableRenderKind.None, result.Kind);
	}

	[Fact]
	public void Build_FlagFalse_ReturnsNone()
	{
		var result = HorizontalTimetableContentBuilder.Build(MakeWork(hasETrain: false, content: new byte[] { 1 }));
		Assert.Equal(HorizontalTimetableRenderKind.None, result.Kind);
	}

	[Fact]
	public void Build_NullContent_ReturnsNone()
	{
		var result = HorizontalTimetableContentBuilder.Build(MakeWork(hasETrain: true, content: null));
		Assert.Equal(HorizontalTimetableRenderKind.None, result.Kind);
	}

	#endregion

	#region Build — image content types

	[Fact]
	public void Build_PngContent_ReturnsPngKindWithBase64Payload()
	{
		byte[] bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
		var result = HorizontalTimetableContentBuilder.Build(MakeWork(
			hasETrain: true, contentType: (int)ContentType.PNG, content: bytes));

		Assert.Equal(HorizontalTimetableRenderKind.Png, result.Kind);
		Assert.Equal(Convert.ToBase64String(bytes), result.Payload);
	}

	[Fact]
	public void Build_JpgContent_ReturnsJpgKindWithBase64Payload()
	{
		byte[] bytes = new byte[] { 0xFF, 0xD8, 0xFF };
		var result = HorizontalTimetableContentBuilder.Build(MakeWork(
			hasETrain: true, contentType: (int)ContentType.JPG, content: bytes));

		Assert.Equal(HorizontalTimetableRenderKind.Jpg, result.Kind);
		Assert.Equal(Convert.ToBase64String(bytes), result.Payload);
	}

	[Fact]
	public void Build_NullContentType_DefaultsToPng()
	{
		byte[] bytes = new byte[] { 0x01, 0x02 };
		var result = HorizontalTimetableContentBuilder.Build(MakeWork(
			hasETrain: true, contentType: null, content: bytes));

		Assert.Equal(HorizontalTimetableRenderKind.Png, result.Kind);
		Assert.Equal(Convert.ToBase64String(bytes), result.Payload);
	}

	[Fact]
	public void Build_UnknownContentType_DefaultsToPng()
	{
		byte[] bytes = new byte[] { 0x01, 0x02 };
		var result = HorizontalTimetableContentBuilder.Build(MakeWork(
			hasETrain: true, contentType: 9999, content: bytes));

		Assert.Equal(HorizontalTimetableRenderKind.Png, result.Kind);
		Assert.Equal(Convert.ToBase64String(bytes), result.Payload);
	}

	#endregion

	#region Build — PDF / URI

	[Fact]
	public void Build_PdfContent_ReturnsPdfKindWithBase64Payload()
	{
		byte[] bytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
		var result = HorizontalTimetableContentBuilder.Build(MakeWork(
			hasETrain: true, contentType: (int)ContentType.PDF, content: bytes));

		Assert.Equal(HorizontalTimetableRenderKind.Pdf, result.Kind);
		Assert.Equal(Convert.ToBase64String(bytes), result.Payload);
	}

	[Fact]
	public void Build_UriContent_ReturnsUriResult()
	{
		string url = "https://example.com/timetable.png";
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes(url);
		var result = HorizontalTimetableContentBuilder.Build(MakeWork(
			hasETrain: true, contentType: (int)ContentType.URI, content: bytes));

		Assert.Equal(HorizontalTimetableRenderKind.Uri, result.Kind);
		Assert.Equal(url, result.Payload);
	}

	#endregion
}
