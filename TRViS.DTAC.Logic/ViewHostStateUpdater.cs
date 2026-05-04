namespace TRViS.DTAC.Logic;

/// <summary>
/// Provides methods for updating existing ViewHostState instances.
/// </summary>
internal static class ViewHostStateUpdater
{
	public static void UpdateSelectedWorkGroup(ViewHostState state, string? workGroupName)
	{
		state.SelectedWorkGroup.Name = workGroupName ?? string.Empty;
		state.SelectedWorkGroup.IsChanged = true;
	}

	public static void UpdateSelectedWork(ViewHostState state, string? workName)
	{
		state.SelectedWork.Name = workName ?? string.Empty;
		state.SelectedWork.IsChanged = true;
	}

	public static void UpdateSelectedTrain(ViewHostState state, string? affectDate, int dayCount)
	{
		state.SelectedTrain.AffectDate = affectDate ?? string.Empty;
		state.SelectedTrain.DayCount = dayCount;
		state.SelectedTrain.IsChanged = true;
	}

	public static string FormatAffectDate(DateTime? affectDate, int dayCount)
	{
		try
		{
			if (affectDate.HasValue)
				return affectDate.Value.ToString("yyyy年M月d日");

			return DateOnly.FromDateTime(DateTime.Now).AddDays(-dayCount).ToString("yyyy年M月d日");
		}
		catch
		{
			return string.Empty;
		}
	}

	public static string FormatAffectDateOnly(DateOnly? affectDate, int dayCount)
	{
		try
		{
			if (affectDate.HasValue)
				return affectDate.Value.ToString("yyyy年M月d日");

			return DateOnly.FromDateTime(DateTime.Now).AddDays(-dayCount).ToString("yyyy年M月d日");
		}
		catch
		{
			return string.Empty;
		}
	}

	public static void UpdateAffectDate(PageHeaderState state, string? affectDate)
	{
		state.AffectDateLabelText = affectDate ?? string.Empty;
	}

	public static void MarkWorkGroupProcessed(ViewHostState state)
		=> state.SelectedWorkGroup.IsChanged = false;

	public static void MarkWorkProcessed(ViewHostState state)
		=> state.SelectedWork.IsChanged = false;

	public static void MarkTrainProcessed(ViewHostState state)
		=> state.SelectedTrain.IsChanged = false;

	public static bool HasWorkGroupChanged(ViewHostState state)
		=> state.SelectedWorkGroup.IsChanged;

	public static bool HasWorkChanged(ViewHostState state)
		=> state.SelectedWork.IsChanged;

	public static bool HasTrainChanged(ViewHostState state)
		=> state.SelectedTrain.IsChanged;

	public static bool ShouldApplyTrainData(TRViS.IO.Models.TrainData? trainData, bool isViewHostVisible, bool isVerticalViewMode)
		=> trainData != null && isViewHostVisible && isVerticalViewMode;
}
