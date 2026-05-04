using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.ViewModels;

namespace TRViS.DTAC.Adapters;

internal sealed class VerticalTimetableDataSourceAdapter : IVerticalTimetableDataSource, IDisposable
{
	private readonly VerticalTimetableViewModel _viewModel;
	private ObservableCollection<VerticalTimetableRowModel> _trackedCollection;
	private bool _disposed;

	public IReadOnlyList<bool> IsInfoRowList => _viewModel.CurrentRows.Select(r => r.IsInfoRow).ToList();
	public bool HasAfterRemarksText => _viewModel.AfterRemarksText is not null;
	public bool HasAfterArriveText => _viewModel.AfterArriveText is not null;
	public bool HasNextTrainId => _viewModel.NextTrainId is not null;

	public event EventHandler? RowsChanged;

	public VerticalTimetableDataSourceAdapter(VerticalTimetableViewModel viewModel)
	{
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		_trackedCollection = _viewModel.CurrentRows;

		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_trackedCollection.CollectionChanged += OnCurrentRowsCollectionChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(VerticalTimetableViewModel.CurrentRows))
			return;

		_trackedCollection.CollectionChanged -= OnCurrentRowsCollectionChanged;
		_trackedCollection = _viewModel.CurrentRows;
		_trackedCollection.CollectionChanged += OnCurrentRowsCollectionChanged;

		RowsChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnCurrentRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		=> RowsChanged?.Invoke(this, EventArgs.Empty);

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		_trackedCollection.CollectionChanged -= OnCurrentRowsCollectionChanged;
	}
}
