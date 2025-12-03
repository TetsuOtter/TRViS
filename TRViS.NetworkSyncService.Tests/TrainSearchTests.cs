using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using TRViS.NetworkSyncService;

namespace TRViS.NetworkSyncService.Tests;

[TestFixture]
public class TrainSearchTests
{
	[Test]
	public void TrainSearchRequest_SerializesCorrectly()
	{
		// Arrange
		var request = new TrainSearchRequest
		{
			TrainNumber = "1234",
			RequestId = "test-request-id"
		};

		// Act
		string json = JsonSerializer.Serialize(request);
		var deserialized = JsonSerializer.Deserialize<TrainSearchRequest>(json);

		// Assert
		Assert.That(deserialized, Is.Not.Null);
		Assert.That(deserialized!.MessageType, Is.EqualTo("SearchTrain"));
		Assert.That(deserialized.TrainNumber, Is.EqualTo("1234"));
		Assert.That(deserialized.RequestId, Is.EqualTo("test-request-id"));
	}

	[Test]
	public void TrainSearchResponse_DeserializesSuccessResponse()
	{
		// Arrange
		string json = @"{
			""MessageType"": ""SearchTrainResult"",
			""RequestId"": ""test-request-id"",
			""Success"": true,
			""Results"": [
				{
					""TrainId"": ""train_123"",
					""TrainNumber"": ""1234"",
					""WorkId"": ""work_1"",
					""WorkName"": ""行路1"",
					""StartStation"": ""東京"",
					""EndStation"": ""大阪"",
					""StartTime"": ""09:00"",
					""EndTime"": ""12:30""
				}
			]
		}";

		// Act
		var response = JsonSerializer.Deserialize<TrainSearchResponse>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		// Assert
		Assert.That(response, Is.Not.Null);
		Assert.That(response!.Success, Is.True);
		Assert.That(response.RequestId, Is.EqualTo("test-request-id"));
		Assert.That(response.Results, Is.Not.Null);
		Assert.That(response.Results!.Length, Is.EqualTo(1));
		Assert.That(response.Results[0].TrainNumber, Is.EqualTo("1234"));
		Assert.That(response.Results[0].WorkName, Is.EqualTo("行路1"));
	}

	[Test]
	public void TrainSearchResponse_DeserializesErrorResponse()
	{
		// Arrange
		string json = @"{
			""MessageType"": ""SearchTrainResult"",
			""RequestId"": ""test-request-id"",
			""Success"": false,
			""ErrorMessage"": ""Train not found""
		}";

		// Act
		var response = JsonSerializer.Deserialize<TrainSearchResponse>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		// Assert
		Assert.That(response, Is.Not.Null);
		Assert.That(response!.Success, Is.False);
		Assert.That(response.ErrorMessage, Is.EqualTo("Train not found"));
		Assert.That(response.Results, Is.Null);
	}

	[Test]
	public void GetTrainDataRequest_SerializesCorrectly()
	{
		// Arrange
		var request = new GetTrainDataRequest
		{
			TrainId = "train_123",
			WorkId = "work_1",
			RequestId = "test-request-id"
		};

		// Act
		string json = JsonSerializer.Serialize(request);
		var deserialized = JsonSerializer.Deserialize<GetTrainDataRequest>(json);

		// Assert
		Assert.That(deserialized, Is.Not.Null);
		Assert.That(deserialized!.MessageType, Is.EqualTo("GetTrainData"));
		Assert.That(deserialized.TrainId, Is.EqualTo("train_123"));
		Assert.That(deserialized.WorkId, Is.EqualTo("work_1"));
		Assert.That(deserialized.RequestId, Is.EqualTo("test-request-id"));
	}

	[Test]
	public void TrainDataResponse_DeserializesSuccessResponse()
	{
		// Arrange
		string json = "{\"MessageType\":\"TrainData\",\"RequestId\":\"test-request-id\",\"Success\":true,\"TrainId\":\"train_123\",\"WorkId\":\"work_1\",\"Data\":\"{}\"}";

		// Act
		var response = JsonSerializer.Deserialize<TrainDataResponse>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		// Assert
		Assert.That(response, Is.Not.Null);
		Assert.That(response!.Success, Is.True);
		Assert.That(response.TrainId, Is.EqualTo("train_123"));
		Assert.That(response.Data, Is.EqualTo("{}"));
	}

	[Test]
	public void GetFeaturesRequest_SerializesCorrectly()
	{
		// Arrange
		var request = new GetFeaturesRequest();

		// Act
		string json = JsonSerializer.Serialize(request);
		var deserialized = JsonSerializer.Deserialize<GetFeaturesRequest>(json);

		// Assert
		Assert.That(deserialized, Is.Not.Null);
		Assert.That(deserialized!.MessageType, Is.EqualTo("GetFeatures"));
	}

	[Test]
	public void FeaturesResponse_DeserializesCorrectly()
	{
		// Arrange
		string json = @"{
			""MessageType"": ""Features"",
			""Features"": [""TrainSearch"", ""SyncedData"", ""Timetable""]
		}";

		// Act
		var response = JsonSerializer.Deserialize<FeaturesResponse>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		// Assert
		Assert.That(response, Is.Not.Null);
		Assert.That(response!.Features, Is.Not.Null);
		Assert.That(response.Features!.Length, Is.EqualTo(3));
		Assert.That(response.Features, Does.Contain("TrainSearch"));
	}

	[Test]
	public void TrainSearchResult_AllPropertiesDeserialize()
	{
		// Arrange
		string json = @"{
			""TrainId"": ""train_123"",
			""TrainNumber"": ""1234"",
			""WorkId"": ""work_1"",
			""WorkName"": ""行路1"",
			""WorkGroupId"": ""wg_1"",
			""StartStation"": ""東京"",
			""EndStation"": ""大阪"",
			""StartTime"": ""09:00"",
			""EndTime"": ""12:30"",
			""Direction"": 0,
			""Destination"": ""大阪""
		}";

		// Act
		var result = JsonSerializer.Deserialize<TrainSearchResult>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		// Assert
		Assert.That(result, Is.Not.Null);
		Assert.That(result!.TrainId, Is.EqualTo("train_123"));
		Assert.That(result.TrainNumber, Is.EqualTo("1234"));
		Assert.That(result.WorkId, Is.EqualTo("work_1"));
		Assert.That(result.WorkName, Is.EqualTo("行路1"));
		Assert.That(result.WorkGroupId, Is.EqualTo("wg_1"));
		Assert.That(result.StartStation, Is.EqualTo("東京"));
		Assert.That(result.EndStation, Is.EqualTo("大阪"));
		Assert.That(result.StartTime, Is.EqualTo("09:00"));
		Assert.That(result.EndTime, Is.EqualTo("12:30"));
		Assert.That(result.Direction, Is.EqualTo(0));
		Assert.That(result.Destination, Is.EqualTo("大阪"));
	}
}
