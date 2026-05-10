using System.Text.Json;

using TRViS.IO.Models;

using JsonModels = TRViS.JsonModels;

namespace TRViS.IO.Tests;

public class JsonModelsConverterTests
{
	static readonly JsonSerializerOptions opts = new()
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

		var trainJson = JsonSerializer.Deserialize<JsonModels.TrainData>(json, opts);
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

		var trainJson = JsonSerializer.Deserialize<JsonModels.TrainData>(json, opts);
		Assert.That(trainJson, Is.Not.Null);

		TrainData converted = JsonModelsConverter.ConvertTrain(trainJson!);

		Assert.That(converted.Rows, Is.Not.Null);
		Assert.That(converted.Rows!, Has.Length.EqualTo(1));
		Assert.That(converted.Rows![0].IsInfoRow, Is.False);
	}
}
