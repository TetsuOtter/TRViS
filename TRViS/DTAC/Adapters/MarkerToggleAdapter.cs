using TRViS.DTAC.Logic.Abstractions;
using TRViS.ViewModels;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps DTACMarkerViewModel to implement IMarkerToggleController.
/// </summary>
internal class MarkerToggleAdapter : IMarkerToggleController
{
	private readonly DTACMarkerViewModel _viewModel;

	public MarkerToggleAdapter(DTACMarkerViewModel viewModel)
	{
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
	}

	public void ResetToggle()
	{
		_viewModel.IsToggled = false;
	}
}
