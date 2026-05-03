using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.ViewModels;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps EasterEggPageViewModel to implement IEasterEggSettings.
/// </summary>
internal class EasterEggSettingsAdapter : IEasterEggSettings
{
	private readonly EasterEggPageViewModel _viewModel;

	public EasterEggSettingsAdapter(EasterEggPageViewModel viewModel)
	{
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		PropertyChanged?.Invoke(this, e);
	}

	public bool KeepScreenOnWhenRunning => _viewModel.KeepScreenOnWhenRunning;

	public bool ShowMapWhenLandscape => _viewModel.ShowMapWhenLandscape;

	public event PropertyChangedEventHandler? PropertyChanged;
}
