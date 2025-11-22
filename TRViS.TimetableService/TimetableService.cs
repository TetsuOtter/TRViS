namespace TRViS.TimetableService;

/// <summary>
/// Implementation of timetable service that manages train data and timetable rows
/// with support for insertions and deletions while maintaining IDs
/// </summary>
public class TimetableService : ITimetableService
{
	private readonly Dictionary<string, TrainDataItem> _trainDataStore = new();
	private readonly object _lock = new();

	public TrainDataItem? GetTrainData(string trainDataId)
	{
		lock (_lock)
		{
			return _trainDataStore.TryGetValue(trainDataId, out var data) ? data : null;
		}
	}

	public IReadOnlyList<TrainDataItem> GetAllTrainData()
	{
		lock (_lock)
		{
			return _trainDataStore.Values.ToList();
		}
	}

	public void SetTrainData(TrainDataItem trainData)
	{
		if (trainData == null)
			throw new ArgumentNullException(nameof(trainData));
		if (string.IsNullOrEmpty(trainData.Id))
			throw new ArgumentException("TrainData ID cannot be null or empty", nameof(trainData));

		lock (_lock)
		{
			_trainDataStore[trainData.Id] = trainData;
		}
	}

	public bool RemoveTrainData(string trainDataId)
	{
		if (string.IsNullOrEmpty(trainDataId))
			throw new ArgumentException("TrainData ID cannot be null or empty", nameof(trainDataId));

		lock (_lock)
		{
			return _trainDataStore.Remove(trainDataId);
		}
	}

	public TimetableRowItem? GetTimetableRow(string trainDataId, string rowId)
	{
		if (string.IsNullOrEmpty(trainDataId))
			throw new ArgumentException("TrainData ID cannot be null or empty", nameof(trainDataId));
		if (string.IsNullOrEmpty(rowId))
			throw new ArgumentException("Row ID cannot be null or empty", nameof(rowId));

		lock (_lock)
		{
			if (!_trainDataStore.TryGetValue(trainDataId, out var trainData))
				return null;

			return trainData.Rows.FirstOrDefault(r => r.Id == rowId);
		}
	}

	public IReadOnlyList<TimetableRowItem> GetTimetableRows(string trainDataId)
	{
		if (string.IsNullOrEmpty(trainDataId))
			throw new ArgumentException("TrainData ID cannot be null or empty", nameof(trainDataId));

		lock (_lock)
		{
			if (!_trainDataStore.TryGetValue(trainDataId, out var trainData))
				return Array.Empty<TimetableRowItem>();

			return trainData.Rows.ToList();
		}
	}

	public void InsertTimetableRow(string trainDataId, int position, TimetableRowItem row)
	{
		if (string.IsNullOrEmpty(trainDataId))
			throw new ArgumentException("TrainData ID cannot be null or empty", nameof(trainDataId));
		if (row == null)
			throw new ArgumentNullException(nameof(row));
		if (string.IsNullOrEmpty(row.Id))
			throw new ArgumentException("Row ID cannot be null or empty", nameof(row));

		lock (_lock)
		{
			if (!_trainDataStore.TryGetValue(trainDataId, out var trainData))
				throw new InvalidOperationException($"TrainData with ID '{trainDataId}' not found");

			if (position < 0 || position > trainData.Rows.Count)
				throw new ArgumentOutOfRangeException(nameof(position), "Position is out of range");

			trainData.Rows.Insert(position, row);
		}
	}

	public void UpdateTimetableRow(string trainDataId, string rowId, TimetableRowItem row)
	{
		if (string.IsNullOrEmpty(trainDataId))
			throw new ArgumentException("TrainData ID cannot be null or empty", nameof(trainDataId));
		if (string.IsNullOrEmpty(rowId))
			throw new ArgumentException("Row ID cannot be null or empty", nameof(rowId));
		if (row == null)
			throw new ArgumentNullException(nameof(row));
		if (string.IsNullOrEmpty(row.Id))
			throw new ArgumentException("Row ID cannot be null or empty", nameof(row));

		lock (_lock)
		{
			if (!_trainDataStore.TryGetValue(trainDataId, out var trainData))
				throw new InvalidOperationException($"TrainData with ID '{trainDataId}' not found");

			var index = trainData.Rows.FindIndex(r => r.Id == rowId);
			if (index < 0)
				throw new InvalidOperationException($"Row with ID '{rowId}' not found");

			trainData.Rows[index] = row;
		}
	}

	public bool RemoveTimetableRow(string trainDataId, string rowId)
	{
		if (string.IsNullOrEmpty(trainDataId))
			throw new ArgumentException("TrainData ID cannot be null or empty", nameof(trainDataId));
		if (string.IsNullOrEmpty(rowId))
			throw new ArgumentException("Row ID cannot be null or empty", nameof(rowId));

		lock (_lock)
		{
			if (!_trainDataStore.TryGetValue(trainDataId, out var trainData))
				return false;

			var row = trainData.Rows.FirstOrDefault(r => r.Id == rowId);
			if (row == null)
				return false;

			return trainData.Rows.Remove(row);
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_trainDataStore.Clear();
		}
	}
}
