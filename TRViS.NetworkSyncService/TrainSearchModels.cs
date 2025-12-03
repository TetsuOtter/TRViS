using System.Text.Json.Serialization;

namespace TRViS.NetworkSyncService;

/// <summary>
/// Request to search for trains by train number
/// </summary>
public class TrainSearchRequest
{
	[JsonPropertyName("MessageType")]
	public string MessageType { get; set; } = "SearchTrain";

	[JsonPropertyName("TrainNumber")]
	public string TrainNumber { get; set; } = string.Empty;

	[JsonPropertyName("RequestId")]
	public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Response from server with train search results
/// </summary>
public class TrainSearchResponse
{
	[JsonPropertyName("MessageType")]
	public string MessageType { get; set; } = "SearchTrainResult";

	[JsonPropertyName("RequestId")]
	public string RequestId { get; set; } = string.Empty;

	[JsonPropertyName("Success")]
	public bool Success { get; set; }

	[JsonPropertyName("Results")]
	public TrainSearchResult[]? Results { get; set; }

	[JsonPropertyName("ErrorMessage")]
	public string? ErrorMessage { get; set; }
}

/// <summary>
/// Single train search result
/// </summary>
public class TrainSearchResult
{
	[JsonPropertyName("TrainId")]
	public string TrainId { get; set; } = string.Empty;

	[JsonPropertyName("TrainNumber")]
	public string TrainNumber { get; set; } = string.Empty;

	[JsonPropertyName("WorkId")]
	public string WorkId { get; set; } = string.Empty;

	[JsonPropertyName("WorkName")]
	public string? WorkName { get; set; }

	[JsonPropertyName("WorkGroupId")]
	public string? WorkGroupId { get; set; }

	[JsonPropertyName("StartStation")]
	public string? StartStation { get; set; }

	[JsonPropertyName("EndStation")]
	public string? EndStation { get; set; }

	[JsonPropertyName("StartTime")]
	public string? StartTime { get; set; }

	[JsonPropertyName("EndTime")]
	public string? EndTime { get; set; }

	[JsonPropertyName("Direction")]
	public int? Direction { get; set; }

	[JsonPropertyName("Destination")]
	public string? Destination { get; set; }
}

/// <summary>
/// Request to get full train data
/// </summary>
public class GetTrainDataRequest
{
	[JsonPropertyName("MessageType")]
	public string MessageType { get; set; } = "GetTrainData";

	[JsonPropertyName("TrainId")]
	public string TrainId { get; set; } = string.Empty;

	[JsonPropertyName("WorkId")]
	public string? WorkId { get; set; }

	[JsonPropertyName("RequestId")]
	public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Response with full train data
/// </summary>
public class TrainDataResponse
{
	[JsonPropertyName("MessageType")]
	public string MessageType { get; set; } = "TrainData";

	[JsonPropertyName("RequestId")]
	public string RequestId { get; set; } = string.Empty;

	[JsonPropertyName("Success")]
	public bool Success { get; set; }

	[JsonPropertyName("TrainId")]
	public string? TrainId { get; set; }

	[JsonPropertyName("WorkId")]
	public string? WorkId { get; set; }

	[JsonPropertyName("Data")]
	public string? Data { get; set; }

	[JsonPropertyName("ErrorMessage")]
	public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to get list of supported features
/// </summary>
public class GetFeaturesRequest
{
	[JsonPropertyName("MessageType")]
	public string MessageType { get; set; } = "GetFeatures";
}

/// <summary>
/// Response with list of supported features
/// </summary>
public class FeaturesResponse
{
	[JsonPropertyName("MessageType")]
	public string MessageType { get; set; } = "Features";

	[JsonPropertyName("Features")]
	public string[]? Features { get; set; }
}
