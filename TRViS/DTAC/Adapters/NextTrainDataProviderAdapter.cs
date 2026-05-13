using TRViS.DTAC.Logic.Abstractions;
using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps AppViewModel to implement INextTrainDataProvider.
/// Delegates GetTrainData to AppViewModel.Loader and SelectTrainData to AppViewModel.SelectedTrainData.
/// </summary>
internal class NextTrainDataProviderAdapter : INextTrainDataProvider
{
	private readonly AppViewModel _viewModel;

	public NextTrainDataProviderAdapter(AppViewModel viewModel)
	{
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
	}

	/// <inheritdoc/>
	public TrainData? GetTrainData(string id)
		=> _viewModel.Loader?.GetTrainData(id);

	/// <inheritdoc/>
	public void SelectTrainData(TrainData? data)
		=> _viewModel.SelectedTrainData = data;
}
