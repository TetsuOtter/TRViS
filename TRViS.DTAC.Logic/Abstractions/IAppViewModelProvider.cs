using System.ComponentModel;

using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Provides access to application-level business state.
/// </summary>
public interface IAppViewModelProvider
{
	/// <summary>
	/// The currently selected work group.
	/// </summary>
	WorkGroup? SelectedWorkGroup { get; }

	/// <summary>
	/// The currently selected work.
	/// </summary>
	Work? SelectedWork { get; }

	/// <summary>
	/// The currently selected train data.
	/// </summary>
	TrainData? SelectedTrainData { get; }

	/// <summary>
	/// サーバーから指定された、タイトルバー時刻表示のフォーマット。
	/// 例: "HH:mm:ss" / "HH:mm" / null は実装側既定 ("HH:mm:ss")。
	/// </summary>
	string? HeaderTimeFormat { get; }

	/// <summary>
	/// Fired when any property changes.
	/// </summary>
	event PropertyChangedEventHandler? PropertyChanged;
}
