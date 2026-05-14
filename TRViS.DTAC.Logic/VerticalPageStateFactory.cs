namespace TRViS.DTAC.Logic;

using TRViS.IO.Models;
using TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Factory for creating VerticalPageState instances.
/// </summary>
internal static class VerticalPageStateFactory
{
	/// <summary>
	/// Creates an initial VerticalPageState from train data.
	/// </summary>
	/// <param name="trainData">The train data to create state from</param>
	/// <param name="affectDate">The affect date string</param>
	/// <param name="isLocationServiceEnabled">Whether location service is enabled</param>
	/// <returns>A new VerticalPageState instance</returns>
	public static VerticalPageState CreateStateFromTrainData(
		TrainData? trainData,
		string? affectDate,
		bool isLocationServiceEnabled)
	{
		var state = new VerticalPageState();
		state.LocationServiceState.IsEnabled = isLocationServiceEnabled;
		state.PageHeaderState.IsLocationServiceEnabled = isLocationServiceEnabled;
		ApplyTrainDataFields(state, trainData, affectDate);
		return state;
	}

	/// <summary>
	/// 既存の <see cref="VerticalPageState"/> に TrainData 由来の表示用 field のみを
	/// 上書きする。<c>IsRunning</c> / <c>IsRunStarted</c> / <c>IsLocationServiceEnabled</c>
	/// など、表示外の運行状態は触らない。
	///
	/// 同 Id + 同行数の WS リアルタイム編集 (soft 更新) と、新規列車選択時の初期化
	/// (= <see cref="CreateStateFromTrainData"/>) の両方からこの helper を呼ぶことで、
	/// 「表示用 field を作る」ロジックを一箇所にまとめている。
	/// </summary>
	public static void ApplyTrainDataFields(VerticalPageState state, TrainData? trainData, string? affectDate)
	{
		if (trainData is null)
		{
			state.PageHeaderState.AffectDateLabelText = affectDate ?? string.Empty;
			return;
		}

		VerticalPageStateUpdater.UpdateDestinationState(state.Destination, trainData.Destination);

		state.TrainInfoAreaState.TrainInfoText = trainData.TrainInfo ?? string.Empty;
		state.TrainInfoAreaState.BeforeDepartureText = trainData.BeforeDeparture ?? string.Empty;

		VerticalPageStateUpdater.UpdateNextDayIndicatorState(state.NextDayIndicatorState, trainData.DayCount);

		state.TrainDisplayInfo.TrainNumber = trainData.TrainNumber ?? string.Empty;
		state.TrainDisplayInfo.CarCount = trainData.CarCount;
		state.TrainDisplayInfo.MaxSpeed = trainData.MaxSpeed ?? string.Empty;
		state.TrainDisplayInfo.SpeedType = trainData.SpeedType ?? string.Empty;
		state.TrainDisplayInfo.NominalTractiveCapacity = trainData.NominalTractiveCapacity ?? string.Empty;
		state.TrainDisplayInfo.BeginRemarks = trainData.BeginRemarks ?? string.Empty;

		state.PageHeaderState.AffectDateLabelText = affectDate ?? string.Empty;
	}

	/// <summary>
	/// 既存の <see cref="VerticalPageState.RowStates"/> エントリに対し、TimetableRow 側の
	/// <c>IsInfoRow</c> フラグだけを上書きする。同 Id + 同行数の soft 更新で使う。
	/// 行数が一致しない場合は何もしない (= 呼び出し側で全面再構築すべきケース)。
	/// </summary>
	public static void SyncRowStatesIsInfoRow(VerticalPageState state, TimetableRow[] rows)
	{
		if (state.RowStates.Count != rows.Length)
			return;
		for (int i = 0; i < rows.Length; i++)
		{
			if (state.RowStates.TryGetValue(i, out var rowState))
				rowState.IsInfoRow = rows[i].IsInfoRow;
		}
	}

	/// <summary>
	/// Creates an empty VerticalPageState.
	/// </summary>
	/// <returns>A new empty VerticalPageState</returns>
	public static VerticalPageState CreateEmptyState()
	{
		return new VerticalPageState
		{
			Destination = new(),
			TrainInfoAreaState = new(),
			NextDayIndicatorState = new(),
			TimetableViewState = new(),
			LocationServiceState = new(),
			PageHeaderState = new(),
			TrainDisplayInfo = new(),
			RowStates = new()
		};
	}

	/// <summary>
	/// Initializes row states from the timetable rows, preserving per-row IsInfoRow.
	/// </summary>
	public static void InitializeRowStates(VerticalPageState pageState, TimetableRow[] rows)
	{
		pageState.RowStates.Clear();
		for (int i = 0; i < rows.Length; i++)
		{
			pageState.RowStates[i] = new VerticalTimetableRowState { IsInfoRow = rows[i].IsInfoRow };
		}
	}

	/// <summary>
	/// Determines if train data should be applied based on ViewHost visibility.
	/// </summary>
	/// <param name="trainData">The train data to check</param>
	/// <param name="isViewHostVisible">Whether the ViewHost is visible</param>
	/// <param name="isVerticalViewMode">Whether vertical view mode is active</param>
	/// <returns>True if should apply, false if should lazy load</returns>
	public static bool ShouldApplyTrainData(TrainData? trainData, bool isViewHostVisible, bool isVerticalViewMode)
	{
		return trainData != null && isViewHostVisible && isVerticalViewMode;
	}

	/// <summary>
	/// Gets train data information for state creation.
	/// </summary>
	/// <param name="trainData">The train data object</param>
	/// <returns>A tuple of (destination, trainInfo, beforeDeparture, dayCount)</returns>
	public static (string? Destination, string TrainInfo, string BeforeDeparture, int DayCount) GetTrainDataInfo(TrainData? trainData)
	{
		if (trainData == null)
		{
			return (null, string.Empty, string.Empty, 0);
		}

		var destination = trainData.Destination;
		var trainInfo = trainData.TrainInfo ?? string.Empty;
		var beforeDeparture = trainData.BeforeDeparture ?? string.Empty;
		var dayCount = trainData.DayCount;

		return (destination, trainInfo, beforeDeparture, dayCount);
	}

	/// <summary>
	/// Gets the rows from train data.
	/// </summary>
	/// <param name="trainData">The train data object</param>
	/// <returns>The rows, or null if not available</returns>
	public static TimetableRow[]? GetTrainDataRows(TrainData? trainData)
	{
		return trainData?.Rows;
	}
}
