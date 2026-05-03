using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;

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
	private readonly IDtacCrashLogger _crashLogger;

	private HakoPageState _currentState = new();

	private string? _workName;
	private string? _workSpaceName;

	private bool _disposed = false;

	public HakoPageState CurrentState => _currentState;

	public event EventHandler<HakoStateChangedEventArgs>? StateChanged;

	public HakoPresenter(IAppViewModelProvider appViewModel, IDtacCrashLogger crashLogger)
	{
		_appViewModel = appViewModel ?? throw new ArgumentNullException(nameof(appViewModel));
		_crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));

		_appViewModel.PropertyChanged += OnAppViewModelPropertyChanged;

		ApplyInitialState();
	}

	private void ApplyInitialState()
	{
		_workName = _appViewModel.SelectedWork?.Name;
		_workSpaceName = _appViewModel.SelectedWorkGroup?.Name;
		_currentState.WorkInfoText = BuildWorkInfoText();

		string affectDate = ViewHostStateFactory.FormatAffectDateOnly(
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
		string affectDate = ViewHostStateFactory.FormatAffectDateOnly(
			trainData?.AffectDate,
			trainData?.DayCount ?? 0);
		_currentState.AffectDateText = AffectDateLabelTextPrefix + affectDate;
		RaiseStateChanged(HakoStateSection.AffectDate);
	}

	// ---------- Intents from View ----------

	/// <summary>
	/// Called when the SimpleView IsBusy state changes.
	/// Updates <see cref="HakoPageState.IsSimpleViewBusy"/> and raises StateChanged.
	/// Animation is handled by the View; this only tracks the logical state.
	/// </summary>
	public void OnSimpleViewBusyChanged(bool isBusy)
	{
		_currentState.IsSimpleViewBusy = isBusy;
		RaiseStateChanged(HakoStateSection.IsSimpleViewBusy);
	}

	/// <summary>
	/// Logs an exception that occurred in View code (e.g. animation failure).
	/// Routes through the crash logger adapter without exposing InstanceManager to the View.
	/// </summary>
	public void LogException(Exception ex, string? context = null)
	{
		_crashLogger.Log(ex, context);
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
