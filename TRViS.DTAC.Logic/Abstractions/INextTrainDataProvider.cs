using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Provides train data lookup and selection for the "next train" feature.
/// </summary>
public interface INextTrainDataProvider
{
	/// <summary>
	/// Retrieves train data for the given ID from the current loader.
	/// Returns null if no loader is available or the ID is not found.
	/// May throw if the underlying data source raises an error.
	/// </summary>
	TrainData? GetTrainData(string id);

	/// <summary>
	/// Sets the currently selected train data (navigates to that train's timetable).
	/// </summary>
	void SelectTrainData(TrainData? data);
}
