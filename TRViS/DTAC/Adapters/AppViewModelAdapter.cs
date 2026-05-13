using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.ViewModels;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps AppViewModel to implement IAppViewModelProvider.
/// </summary>
internal class AppViewModelAdapter : IAppViewModelProvider
{
    private readonly AppViewModel _viewModel;

    public AppViewModelAdapter(AppViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

    public TRViS.IO.Models.WorkGroup? SelectedWorkGroup => _viewModel.SelectedWorkGroup;
    public TRViS.IO.Models.Work? SelectedWork => _viewModel.SelectedWork;
    public TRViS.IO.Models.TrainData? SelectedTrainData => _viewModel.SelectedTrainData;
    public string? HeaderTimeFormat => _viewModel.HeaderTimeFormat;

    public event PropertyChangedEventHandler? PropertyChanged;
}
