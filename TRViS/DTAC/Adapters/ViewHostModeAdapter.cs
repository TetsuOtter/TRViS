using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.ViewModels;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps DTACViewHostViewModel to implement IViewHostModeProvider, IViewHostNavigationSink, and ITabModeController.
/// </summary>
internal class ViewHostModeAdapter : IViewHostModeProvider, IViewHostNavigationSink, ITabModeController
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

	public DTACTabMode TabMode => _viewModel.TabMode switch
	{
		DTACViewHostViewModel.Mode.Hako => DTACTabMode.Hako,
		DTACViewHostViewModel.Mode.VerticalView => DTACTabMode.VerticalView,
		DTACViewHostViewModel.Mode.WorkAffix => DTACTabMode.WorkAffix,
		_ => DTACTabMode.None,
	};

	public bool IsHakoMode => _viewModel.IsHakoMode;

	public bool IsWorkAffixMode => _viewModel.IsWorkAffixMode;

	/// <summary>
	/// Called when Shell navigation determines whether ViewHost is the current page.
	/// </summary>
	public void NotifyNavigated(bool isCurrentPage)
	{
		_viewModel.IsViewHostVisible = isCurrentPage;
	}

	/// <summary>
	/// Forces the active tab mode to Hako.
	/// Called when a tab button becomes disabled while selected.
	/// </summary>
	public void ResetToHako()
	{
		_viewModel.TabMode = DTACViewHostViewModel.Mode.Hako;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
}
