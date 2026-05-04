using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Presenter for the Hako page.
/// Contains all business logic for formatting and state management.
/// </summary>
public sealed class HakoPresenter : IDisposable
{
	/// <summary>
	/// Prefix used before the affect-date value in the label.
	/// </summary>
	public const string AffectDateLabelTextPrefix = "行路施行日\n";

	private readonly IAppViewModelProvider _appViewModel;

	private HakoPageState _currentState = new();

	private string? _workName;
	private string? _workSpaceName;

	private bool _disposed = false;

	public HakoPageState CurrentState => _currentState;

	public event EventHandler<HakoStateChangedEventArgs>? StateChanged;

	public HakoPresenter(IAppViewModelProvider appViewModel)
	{
		_appViewModel = appViewModel ?? throw new ArgumentNullException(nameof(appViewModel));

		_appViewModel.PropertyChanged += OnAppViewModelPropertyChanged;

		ApplyInitialState();
	}

	private void ApplyInitialState()
	{
		_workName = _appViewModel.SelectedWork?.Name;
		_workSpaceName = _appViewModel.SelectedWorkGroup?.Name;
		_currentState.WorkInfoText = BuildWorkInfoText();

		string affectDate = AffectDateFormatter.FormatAffectDateOnly(
			_appViewModel.SelectedTrainData?.AffectDate,
			_appViewModel.SelectedTrainData?.DayCount ?? 0);
		_currentState.AffectDateText = AffectDateLabelTextPrefix + affectDate;
	}

	private void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(IAppViewModelProvider.SelectedWork):
				_workName = _appViewModel.SelectedWork?.Name;
				UpdateWorkInfoText();
				break;
			case nameof(IAppViewModelProvider.SelectedWorkGroup):
				_workSpaceName = _appViewModel.SelectedWorkGroup?.Name;
				UpdateWorkInfoText();
				break;
			case nameof(IAppViewModelProvider.SelectedTrainData):
				OnTrainDataChanged();
				break;
		}
	}

	private void OnTrainDataChanged()
	{
		var trainData = _appViewModel.SelectedTrainData;
		string affectDate = AffectDateFormatter.FormatAffectDateOnly(
			trainData?.AffectDate,
			trainData?.DayCount ?? 0);
		_currentState.AffectDateText = AffectDateLabelTextPrefix + affectDate;
		RaiseStateChanged(HakoStateSection.AffectDate);
	}

	// ---------- Helpers ----------

	private string BuildWorkInfoText()
	{
		if (string.IsNullOrEmpty(_workName) && string.IsNullOrEmpty(_workSpaceName))
			return string.Empty;
		return $"{_workName}\n{_workSpaceName}";
	}

	private void UpdateWorkInfoText()
	{
		_currentState.WorkInfoText = BuildWorkInfoText();
		RaiseStateChanged(HakoStateSection.WorkInfo);
	}

	private void RaiseStateChanged(HakoStateSection changed)
	{
		StateChanged?.Invoke(this, new HakoStateChangedEventArgs(changed));
	}

	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;
		_appViewModel.PropertyChanged -= OnAppViewModelPropertyChanged;
	}
}
