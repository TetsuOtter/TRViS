namespace TRViS.TimetableService;

/// <summary>
/// Service interface for managing timetable data with support for insertion and deletion
/// </summary>
public interface ITimetableService
{
	/// <summary>
	/// Get a train data by its ID
	/// </summary>
	TrainDataItem? GetTrainData(string trainDataId);

	/// <summary>
	/// Get all train data
	/// </summary>
	IReadOnlyList<TrainDataItem> GetAllTrainData();

	/// <summary>
	/// Add or update a train data
	/// </summary>
	void SetTrainData(TrainDataItem trainData);

	/// <summary>
	/// Remove a train data by its ID
	/// </summary>
	bool RemoveTrainData(string trainDataId);

	/// <summary>
	/// Get a timetable row by its ID from a specific train
	/// </summary>
	TimetableRowItem? GetTimetableRow(string trainDataId, string rowId);

	/// <summary>
	/// Get all timetable rows for a specific train
	/// </summary>
	IReadOnlyList<TimetableRowItem> GetTimetableRows(string trainDataId);

	/// <summary>
	/// Insert a timetable row at a specific position
	/// </summary>
	void InsertTimetableRow(string trainDataId, int position, TimetableRowItem row);

	/// <summary>
	/// Update a timetable row
	/// </summary>
	void UpdateTimetableRow(string trainDataId, string rowId, TimetableRowItem row);

	/// <summary>
	/// Remove a timetable row by its ID
	/// </summary>
	bool RemoveTimetableRow(string trainDataId, string rowId);

	/// <summary>
	/// Clear all data
	/// </summary>
	void Clear();
}
