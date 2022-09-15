using System.Text.Json;
using System.Text.Json.Serialization;
using SQLite;
using TRViS.IO.Models;

namespace TRViS.IO;

public class LoaderSQL : IDisposable
{
	SQLiteConnection Connection { get; }

	public LoaderSQL(string path)
	{
		Connection = new(path);
	}

	public IReadOnlyList<TrainDataGroup> LoadFromSQLite()
	{
		List<TrainDataGroup> result = new();

		var res =
			from g in Connection.Table<Models.DB.WorkGroup>()
			join n in Connection.Table<Models.DB.Work>()
			on g.Id equals n.WorkGroupId
			join t in Connection.Table<Models.DB.TrainData>()
			on n.Id equals t.WorkId
			select new
			{
				GroupId = g.Id,
				GroupName = g.Name,
				WorkId = n.Id,
				WorkName = n.Name,
				TrainId = t.Id,
				TrainNumber = t.TrainNumber
			};

		foreach (var group in res.GroupBy(v => v.GroupId))
		{
			List<TrainDataFileInfo> fileInfo = new();
			foreach (var work in group)
				fileInfo.Add(new(work.WorkId.ToString(), work.TrainId.ToString(), work.WorkName, work.TrainNumber));
			result.Add(new(group.Key.ToString(), group.FirstOrDefault()?.GroupName ?? "N/A", fileInfo.ToArray()));
		}

		return result;
	}

	//public IReadOnlyList<Models.DB.WorkGroup> GetWorkGroupListAsync()

	public void Dispose()
	{
		Connection.Dispose();
	}
}
