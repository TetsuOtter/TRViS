using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Presenter state for the NextTrainButton view.
/// </summary>
public record NextTrainButtonState(
	bool IsVisible,
	string ButtonText,
	string CurrentNextTrainId);

/// <summary>
/// Presenter that handles next-train-button business logic:
/// looking up train data, formatting button text, and handling click navigation.
/// </summary>
public class NextTrainButtonPresenter
{
	private readonly INextTrainDataProvider _trainDataProvider;
	private readonly IAppViewModelProvider _appViewModelProvider;
	private readonly IDtacCrashLogger _crashLogger;
	private readonly IUserAlertService _userAlertService;

	private NextTrainButtonState _currentState = new(IsVisible: false, ButtonText: string.Empty, CurrentNextTrainId: string.Empty);

	/// <summary>
	/// The current presenter state. Drives the View's display.
	/// </summary>
	public NextTrainButtonState CurrentState => _currentState;

	/// <summary>
	/// Fired whenever the presenter state changes.
	/// </summary>
	public event EventHandler<NextTrainButtonState>? StateChanged;

	public NextTrainButtonPresenter(
		INextTrainDataProvider trainDataProvider,
		IAppViewModelProvider appViewModelProvider,
		IDtacCrashLogger crashLogger,
		IUserAlertService userAlertService)
	{
		_trainDataProvider = trainDataProvider ?? throw new ArgumentNullException(nameof(trainDataProvider));
		_appViewModelProvider = appViewModelProvider ?? throw new ArgumentNullException(nameof(appViewModelProvider));
		_crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));
		_userAlertService = userAlertService ?? throw new ArgumentNullException(nameof(userAlertService));

		_appViewModelProvider.PropertyChanged += OnAppViewModelPropertyChanged;
	}

	private void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(IAppViewModelProvider.SelectedTrainData))
			return;

		string? nextTrainId = _appViewModelProvider.SelectedTrainData?.NextTrainId;
		if (string.IsNullOrEmpty(nextTrainId))
		{
			UpdateState(new NextTrainButtonState(IsVisible: false, ButtonText: string.Empty, CurrentNextTrainId: string.Empty));
			return;
		}

		try
		{
			OnNextTrainIdChanged(nextTrainId);
		}
		catch (Exception ex)
		{
			string msg = NextTrainButtonMessages.FormatSetterErrorMessage(
				workGroupId: _appViewModelProvider.SelectedWorkGroup?.Id,
				workId: _appViewModelProvider.SelectedWork?.Id,
				trainId: _appViewModelProvider.SelectedTrainData?.Id,
				currentNextTrainId: _currentState.CurrentNextTrainId,
				givenNextTrainId: nextTrainId);
			_crashLogger.Log(ex, msg);
			UpdateState(new NextTrainButtonState(IsVisible: false, ButtonText: string.Empty, CurrentNextTrainId: _currentState.CurrentNextTrainId));
		}
	}

	private void OnNextTrainIdChanged(string newNextTrainId)
	{
		TRViS.IO.Models.TrainData? nextTrainData;
		try
		{
			nextTrainData = _trainDataProvider.GetTrainData(newNextTrainId);
		}
		catch (Exception ex)
		{
			string msg = NextTrainButtonMessages.FormatSetterErrorMessage(
				workGroupId: _appViewModelProvider.SelectedWorkGroup?.Id,
				workId: _appViewModelProvider.SelectedWork?.Id,
				trainId: _appViewModelProvider.SelectedTrainData?.Id,
				currentNextTrainId: _currentState.CurrentNextTrainId,
				givenNextTrainId: newNextTrainId);

			_crashLogger.Log(ex, msg);
			UpdateState(new NextTrainButtonState(IsVisible: false, ButtonText: string.Empty, CurrentNextTrainId: _currentState.CurrentNextTrainId));
			return;
		}

		if (nextTrainData is null)
		{
			throw new KeyNotFoundException($"Next TrainData not found (id: {newNextTrainId})");
		}
		else if (nextTrainData.TrainNumber is null)
		{
			throw new NullReferenceException($"Next TrainData has no TrainNumber (id: {newNextTrainId})");
		}

		string buttonText = NextTrainButtonMessages.FormatButtonText(nextTrainData.TrainNumber);
		UpdateState(new NextTrainButtonState(IsVisible: true, ButtonText: buttonText, CurrentNextTrainId: newNextTrainId));
	}

	/// <summary>
	/// Called when the next-train button is clicked.
	/// Navigates to the next train's timetable. On error, shows an alert.
	/// </summary>
	public void OnButtonClicked()
	{
		string nextTrainId = _currentState.CurrentNextTrainId;
		if (string.IsNullOrEmpty(nextTrainId))
			return;

		try
		{
			var trainData = _trainDataProvider.GetTrainData(nextTrainId);
			_trainDataProvider.SelectTrainData(trainData);
		}
		catch (Exception ex)
		{
			string msg = NextTrainButtonMessages.FormatClickErrorMessage(
				workGroupId: _appViewModelProvider.SelectedWorkGroup?.Id,
				workId: _appViewModelProvider.SelectedWork?.Id,
				trainId: _appViewModelProvider.SelectedTrainData?.Id,
				nextTrainId: nextTrainId);

			_crashLogger.Log(ex, "NextTrainButton.Click");
			_userAlertService.DisplayAlert("エラー", msg, "OK");
		}
	}

	private void UpdateState(NextTrainButtonState newState)
	{
		_currentState = newState;
		StateChanged?.Invoke(this, newState);
	}
}
