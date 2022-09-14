using System.Text.Json;
using System.Text.Json.Serialization;
using SQLite;
using TRViS.IO.Models;

namespace TRViS.IO;

public class Loader : IDisposable
{
	Dictionary<string, SQLiteConnection> Connections { get; } = new();

	public static ValueTask<TrainData?> LoadFromFile(string path)
		=> LoadFromFile(File.OpenRead(path));

	public static ValueTask<TrainData?> LoadFromFile(Stream stream)
		=> JsonSerializer.DeserializeAsync<TrainData>(stream);

	public IReadOnlyList<TrainDataGroup> LoadFromSQLite(string path)
	{
		List<TrainDataGroup> result = new();

		if (!Connections.TryGetValue(path, out SQLiteConnection? value) || value is null)
		{
			value = new(path);
			Connections[path] = value;
		}

		var res =
			from g in value.Table<Models.DB.WorkGroup>()
			join n in value.Table<Models.DB.Work>()
			on g.Id equals n.WorkGroupId
			join t in value.Table<Models.DB.TrainData>()
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

	public void Dispose(string path)
	{
		if (Connections.TryGetValue(path, out SQLiteConnection? cnx))
		{
			cnx?.Dispose();
			Connections.Remove(path);
		}
	}

	public void Dispose()
	{
		foreach (var v in Connections.Values)
			v.Dispose();
		Connections.Clear();
	}
}
