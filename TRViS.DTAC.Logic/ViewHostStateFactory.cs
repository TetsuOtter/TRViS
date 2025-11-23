namespace TRViS.DTAC.Logic;

using TRViS.IO.Models;

/// <summary>
/// Factory for creating and updating ViewHostState instances.
/// This class contains business logic for managing state transitions
/// in the main ViewHost (DTAC view host).
/// </summary>
public static class ViewHostStateFactory
{
	/// <summary>
	/// Creates an initial empty ViewHostState.
	/// </summary>
	/// <returns>A new ViewHostState instance</returns>
	public static ViewHostState CreateEmptyState()
	{
		return new ViewHostState
		{
			SelectedWorkGroup = new(),
			SelectedWork = new(),
			SelectedTrain = new()
		};
	}

	/// <summary>
	/// Updates the work group state when it changes.
	/// </summary>
	/// <param name="state">The view host state to update</param>
	/// <param name="workGroupName">The name of the work group</param>
	public static void UpdateSelectedWorkGroup(ViewHostState state, string? workGroupName)
	{
		state.SelectedWorkGroup.Name = workGroupName ?? string.Empty;
		state.SelectedWorkGroup.IsChanged = true;
	}

	/// <summary>
	/// Updates the work state when it changes.
	/// </summary>
	/// <param name="state">The view host state to update</param>
	/// <param name="workName">The name of the work</param>
	public static void UpdateSelectedWork(ViewHostState state, string? workName)
	{
		state.SelectedWork.Name = workName ?? string.Empty;
		state.SelectedWork.IsChanged = true;
	}

	/// <summary>
	/// Updates the train state when it changes.
	/// </summary>
	/// <param name="state">The view host state to update</param>
	/// <param name="affectDate">The affect date</param>
	/// <param name="dayCount">The day count for the train</param>
	public static void UpdateSelectedTrain(ViewHostState state, string? affectDate, int dayCount)
	{
		state.SelectedTrain.AffectDate = affectDate ?? string.Empty;
		state.SelectedTrain.DayCount = dayCount;
		state.SelectedTrain.IsChanged = true;
	}

	/// <summary>
	/// Formats the affect date from train data.
	/// </summary>
	/// <param name="affectDate">The affect date (nullable DateTime)</param>
	/// <param name="dayCount">The day count</param>
	/// <returns>The formatted affect date string</returns>
	public static string FormatAffectDate(DateTime? affectDate, int dayCount)
	{
		try
		{
			// If affectDate exists in train data, use it
			if (affectDate.HasValue)
			{
				return affectDate.Value.ToString("yyyy年M月d日");
			}

			// Otherwise calculate from day count
			DateOnly calculatedDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-dayCount);
			return calculatedDate.ToString("yyyy年M月d日");
		}
		catch
		{
			return string.Empty;
		}
	}

	/// <summary>
	/// Marks the work group state as unchanged (after it has been processed).
	/// </summary>
	/// <param name="state">The view host state to update</param>
	public static void MarkWorkGroupProcessed(ViewHostState state)
	{
		state.SelectedWorkGroup.IsChanged = false;
	}

	/// <summary>
	/// Marks the work state as unchanged (after it has been processed).
	/// </summary>
	/// <param name="state">The view host state to update</param>
	public static void MarkWorkProcessed(ViewHostState state)
	{
		state.SelectedWork.IsChanged = false;
	}

	/// <summary>
	/// Marks the train state as unchanged (after it has been processed).
	/// </summary>
	/// <param name="state">The view host state to update</param>
	public static void MarkTrainProcessed(ViewHostState state)
	{
		state.SelectedTrain.IsChanged = false;
	}

	/// <summary>
	/// Determines if train data should be applied to the VerticalStylePage.
	/// </summary>
	/// <param name="trainData">The train data to check (non-null indicates data exists)</param>
	/// <param name="isViewHostVisible">Whether the ViewHost is visible</param>
	/// <param name="isVerticalViewMode">Whether vertical view mode is active</param>
	/// <returns>True if the train data should be applied, false if lazy loading</returns>
	public static bool ShouldApplyTrainData(TrainData? trainData, bool isViewHostVisible, bool isVerticalViewMode)
	{
		return trainData != null && isViewHostVisible && isVerticalViewMode;
	}

	/// <summary>
	/// Updates the ViewHost display state.
	/// </summary>
	/// <param name="state">The vertical page state to update</param>
	/// <param name="isViewHostVisible">Whether the ViewHost is visible</param>
	/// <param name="isVerticalViewMode">Whether vertical view mode is active</param>
	/// <param name="isHakoMode">Whether hako mode is active</param>
	/// <param name="isWorkAffixMode">Whether work affix mode is active</param>
	public static void UpdateViewHostDisplayState(
		VerticalPageState state,
		bool isViewHostVisible,
		bool isVerticalViewMode,
		bool isHakoMode,
		bool isWorkAffixMode)
	{
		state.ViewHostDisplayState.IsVisible = isViewHostVisible;
		state.ViewHostDisplayState.IsVerticalViewMode = isVerticalViewMode;
		state.ViewHostDisplayState.IsHakoMode = isHakoMode;
		state.ViewHostDisplayState.IsWorkAffixMode = isWorkAffixMode;
	}

	/// <summary>
	/// Updates the affect date in the page header.
	/// </summary>
	/// <param name="state">The page header state to update</param>
	/// <param name="affectDate">The affect date string</param>
	public static void UpdateAffectDate(PageHeaderState state, string? affectDate)
	{
		state.AffectDateLabelText = affectDate ?? string.Empty;
	}


	/// <summary>
	/// Determines if a work group changed and should be processed.
	/// </summary>
	/// <param name="state">The view host state</param>
	/// <returns>True if work group changed, false otherwise</returns>
	public static bool HasWorkGroupChanged(ViewHostState state)
	{
		return state.SelectedWorkGroup.IsChanged;
	}

	/// <summary>
	/// Determines if a work changed and should be processed.
	/// </summary>
	/// <param name="state">The view host state</param>
	/// <returns>True if work changed, false otherwise</returns>
	public static bool HasWorkChanged(ViewHostState state)
	{
		return state.SelectedWork.IsChanged;
	}

	/// <summary>
	/// Determines if a train changed and should be processed.
	/// </summary>
	/// <param name="state">The view host state</param>
	/// <returns>True if train changed, false otherwise</returns>
	public static bool HasTrainChanged(ViewHostState state)
	{
		return state.SelectedTrain.IsChanged;
	}
}
