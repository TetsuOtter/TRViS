using System.Text.Json;

using TRViS.IO.Models;

using JsonModels = TRViS.JsonModels;

namespace TRViS.IO.Tests;

public class JsonModelsConverterTests
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		AllowTrailingCommas = true,
		PropertyNameCaseInsensitive = true,
	};

	[TestCase(0, false)]
	[TestCase(1, false)]
	[TestCase(2, true)]
	[TestCase(3, true)]
	public void ConvertTrain_MapsRecordTypeToIsInfoRow(int recordType, bool expected)
	{
		string json = $$"""
			{
			  "Id": "t1",
			  "TrainNumber": "T-001",
			  "Direction": 1,
			  "TimetableRows": [
			    {
			      "Id": "r1",
			      "StationName": "Row",
			      "Location_m": 0.0,
			      "RecordType": {{recordType}}
			    }
			  ]
			}
			""";

		var trainJson = JsonSerializer.Deserialize<JsonModels.TrainData>(json, JsonOptions);
		Assert.That(trainJson, Is.Not.Null);

		TrainData converted = JsonModelsConverter.ConvertTrain(trainJson!);

		Assert.That(converted.Rows, Is.Not.Null);
		Assert.That(converted.Rows!, Has.Length.EqualTo(1));
		Assert.That(converted.Rows![0].IsInfoRow, Is.EqualTo(expected));
	}

	[Test]
	public void ConvertTrain_MissingRecordType_DefaultsToNotInfoRow()
	{
		string json = """
			{
			  "Id": "t1",
			  "TrainNumber": "T-001",
			  "Direction": 1,
			  "TimetableRows": [
			    {
			      "Id": "r1",
			      "StationName": "Row",
			      "Location_m": 0.0
			    }
			  ]
			}
			""";

		var trainJson = JsonSerializer.Deserialize<JsonModels.TrainData>(json, JsonOptions);
		Assert.That(trainJson, Is.Not.Null);

		TrainData converted = JsonModelsConverter.ConvertTrain(trainJson!);

		Assert.That(converted.Rows, Is.Not.Null);
		Assert.That(converted.Rows!, Has.Length.EqualTo(1));
		Assert.That(converted.Rows![0].IsInfoRow, Is.False);
	}

	private static JsonModels.WorkData DeserializeWork(string json)
		=> JsonSerializer.Deserialize<JsonModels.WorkData>(json, JsonOptions)!;

	[Test]
	public void ConvertWork_DecodesETrainTimetableContentBase64()
	{
		// JSON 上の ETrainTimetableContent は base64 文字列として与えられ、
		// Models.Work.ETrainTimetableContent では byte[] にデコードされて格納されること。
		// (WebSocketNetworkSyncService 経由で受信した場合のキャッシュ整合性を保証する)
		var jsonWork = DeserializeWork("""
			{
				"Id": "w-bytes",
				"Name": "W-bytes",
				"HasETrainTimetable": true,
				"ETrainTimetableContentType": 2,
				"ETrainTimetableContent": "aGVsbG8="
			}
			""");

		var work = JsonModelsConverter.ConvertWork(jsonWork, workGroupId: "wg-1");

		Assert.Multiple(() =>
		{
			Assert.That(work.HasETrainTimetable, Is.True);
			Assert.That(work.ETrainTimetableContentType, Is.EqualTo(2));
			Assert.That(work.ETrainTimetableContent, Is.EqualTo(new byte[] { 0x68, 0x65, 0x6C, 0x6C, 0x6F }));
		});
	}

	[Test]
	public void ConvertWork_NullOrEmptyETrainTimetableContent_ReturnsNull()
	{
		var nullContent = DeserializeWork("""
			{ "Id": "w-null", "Name": "W-null", "HasETrainTimetable": false }
			""");
		var emptyContent = DeserializeWork("""
			{ "Id": "w-empty", "Name": "W-empty", "HasETrainTimetable": false, "ETrainTimetableContent": "" }
			""");

		var nullWork = JsonModelsConverter.ConvertWork(nullContent, workGroupId: "wg-1");
		var emptyWork = JsonModelsConverter.ConvertWork(emptyContent, workGroupId: "wg-1");

		Assert.Multiple(() =>
		{
			Assert.That(nullWork.ETrainTimetableContent, Is.Null);
			Assert.That(emptyWork.ETrainTimetableContent, Is.Null);
		});
	}

	[Test]
	public void ConvertWork_InvalidBase64ETrainTimetableContent_ReturnsNull()
	{
		// Base64 として解釈不可能な文字列はサイレントに null として扱う
		// (LoaderJson.DecodeBase64OrNull の挙動と一致させる)
		var jsonWork = DeserializeWork("""
			{
				"Id": "w-bad",
				"Name": "W-bad",
				"HasETrainTimetable": true,
				"ETrainTimetableContent": "not-valid-base64!!!"
			}
			""");

		var work = JsonModelsConverter.ConvertWork(jsonWork, workGroupId: "wg-1");

		Assert.That(work.ETrainTimetableContent, Is.Null);
	}
}
