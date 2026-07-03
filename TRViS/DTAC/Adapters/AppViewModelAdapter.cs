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
    // 検索表示中は検索列車、それ以外は所定の選択列車を返す (EffectiveTrainData)。
    // AppViewModel は検索 override 切替時に SelectedTrainData の PropertyChanged を発火するため、
    // 下流の Presenter は同じ通知で実効列車の変化を検知できる。
    public TRViS.IO.Models.TrainData? SelectedTrainData => _viewModel.EffectiveTrainData;
    public string? HeaderTimeFormat => _viewModel.HeaderTimeFormat;

    public event PropertyChangedEventHandler? PropertyChanged;
}
