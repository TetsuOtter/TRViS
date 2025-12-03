using TRViS.NetworkSyncService;

namespace TRViS.DemoServer.Services;

public class TimetableService
{
private readonly List<SampleTrain> _trains = new();
private readonly object _lock = new();
public bool IsTrainSearchEnabled { get; set; } = true;

public TimetableService()
{
InitializeSampleData();
}

private void InitializeSampleData()
{
_trains.Add(new SampleTrain
{
TrainId = "train_001",
TrainNumber = "1234",
WorkId = "work_1",
WorkName = "行路1",
WorkGroupId = "wg_1",
StartStation = "東京",
EndStation = "大阪",
StartTime = "09:00",
EndTime = "12:30",
Direction = 0,
Destination = "大阪"
});
}

public TrainSearchResponse SearchTrains(string trainNumber)
{
lock (_lock)
{
if (!IsTrainSearchEnabled)
{
return new TrainSearchResponse
{
Success = false,
ErrorMessage = "Train search is currently disabled"
};
}

var results = _trains
.Where(t => t.TrainNumber == trainNumber)
.Select(t => new TrainSearchResult
{
TrainId = t.TrainId,
TrainNumber = t.TrainNumber,
WorkId = t.WorkId,
WorkName = t.WorkName,
WorkGroupId = t.WorkGroupId,
StartStation = t.StartStation,
EndStation = t.EndStation,
StartTime = t.StartTime,
EndTime = t.EndTime,
Direction = t.Direction,
Destination = t.Destination
})
.ToArray();

return new TrainSearchResponse
{
Success = true,
Results = results
};
}
}
}

public class SampleTrain
{
public string TrainId { get; set; } = string.Empty;
public string TrainNumber { get; set; } = string.Empty;
public string WorkId { get; set; } = string.Empty;
public string? WorkName { get; set; }
public string? WorkGroupId { get; set; }
public string? StartStation { get; set; }
public string? EndStation { get; set; }
public string? StartTime { get; set; }
public string? EndTime { get; set; }
public int? Direction { get; set; }
public string? Destination { get; set; }
}
