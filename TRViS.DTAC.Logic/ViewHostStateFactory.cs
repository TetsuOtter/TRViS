namespace TRViS.DTAC.Logic;

/// <summary>
/// Factory for creating ViewHostState instances.
/// </summary>
internal static class ViewHostStateFactory
{
	public static ViewHostState CreateEmptyState()
	{
		return new ViewHostState
		{
			SelectedWorkGroup = new(),
			SelectedWork = new(),
			SelectedTrain = new()
		};
	}
}
