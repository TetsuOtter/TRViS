using TRViS.IO;
using TRViS.IO.Models;

namespace TRViS.Utils;

public class SampleDataLoader : TRViS.IO.ILoader
{
	const string SampleDataFileName = "sample_data.json";

	readonly LoaderJson _loader;

	SampleDataLoader(LoaderJson loader)
	{
		_loader = loader;
	}

	public static async Task<SampleDataLoader> CreateAsync(CancellationToken cancellationToken = default)
	{
		using Stream stream = await FileSystem.OpenAppPackageFileAsync(SampleDataFileName);
		LoaderJson loader = await LoaderJson.InitFromStreamAsync(stream, cancellationToken);
		return new SampleDataLoader(loader);
	}

	public void Dispose() => _loader.Dispose();

	public TrainData? GetTrainData(string trainId)
	{
		try
		{
			return _loader.GetTrainData(trainId);
		}
		catch (KeyNotFoundException)
		{
			return null;
		}
	}

	public IReadOnlyList<WorkGroup> GetWorkGroupList() => _loader.GetWorkGroupList();
	public IReadOnlyList<Work> GetWorkList(string workGroupId) => _loader.GetWorkList(workGroupId);
	public IReadOnlyList<TrainData> GetTrainDataList(string workId) => _loader.GetTrainDataList(workId);
}
