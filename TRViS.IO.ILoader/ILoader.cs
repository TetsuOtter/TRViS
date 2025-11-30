using TRViS.IO.Models;

namespace TRViS.IO;

public interface ILoader : IDisposable
{
	TrainData? GetTrainData(string trainId);

	IReadOnlyList<WorkGroup> GetWorkGroupList();

	IReadOnlyList<Work> GetWorkList(string workGroupId);

	IReadOnlyList<TrainData> GetTrainDataList(string workId);
}
