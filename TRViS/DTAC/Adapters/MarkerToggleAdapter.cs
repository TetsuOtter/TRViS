using System.ComponentModel;

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
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
	}

	// IMarkerToggleController

	public bool IsToggled => _viewModel.IsToggled;

	public void ResetToggle()
	{
		_viewModel.IsToggled = false;
	}

	public void Toggle()
	{
		_viewModel.IsToggled = !_viewModel.IsToggled;
	}

	// INotifyPropertyChanged

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DTACMarkerViewModel.IsToggled))
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsToggled)));
		}
	}
}
