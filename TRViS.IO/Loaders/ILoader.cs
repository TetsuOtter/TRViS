using TRViS.IO.Models;

namespace TRViS.IO;

public interface ILoader : IDisposable
{
	IReadOnlyList<TrainDataGroup> GetTrainDataGroupList();

	TrainData? GetTrainData(string trainId);

	IReadOnlyList<Models.DB.WorkGroup> GetWorkGroupList();

	IReadOnlyList<Models.DB.Work> GetWorkList(string workGroupId);

	IReadOnlyList<Models.DB.TrainData> GetTrainDataList(string workId);
}
