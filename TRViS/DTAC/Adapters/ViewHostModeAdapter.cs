using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.ViewModels;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps DTACViewHostViewModel to implement IViewHostModeProvider.
/// </summary>
internal class ViewHostModeAdapter : IViewHostModeProvider
{
	private readonly DTACViewHostViewModel _viewModel;

	public ViewHostModeAdapter(DTACViewHostViewModel viewModel)
	{
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		PropertyChanged?.Invoke(this, e);
	}

	public bool IsViewHostVisible => _viewModel.IsViewHostVisible;

	public bool IsVerticalViewMode => _viewModel.IsVerticalViewMode;

	public event PropertyChangedEventHandler? PropertyChanged;
}
