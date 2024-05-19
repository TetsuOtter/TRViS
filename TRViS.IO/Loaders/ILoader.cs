using TRViS.IO.Models;

namespace TRViS.IO;

public interface ILoader : IDisposable
{
	IReadOnlyList<TrainDataGroup> GetTrainDataGroupList();

	TrainData? GetTrainData(int trainId);

	IReadOnlyList<Models.DB.WorkGroup> GetWorkGroupList();

	IReadOnlyList<Models.DB.Work> GetWorkList(int workGroupId);

	IReadOnlyList<Models.DB.TrainData> GetTrainDataList(int workId);
}
