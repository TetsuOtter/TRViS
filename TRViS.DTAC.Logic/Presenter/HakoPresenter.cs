using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Presenter for the Hako page.
/// Contains all business logic for formatting and state management.
/// </summary>
public sealed class HakoPresenter
{
	/// <summary>
	/// Prefix used before the affect-date value in the label.
	/// </summary>
	public const string AffectDateLabelTextPrefix = "行路施行日\n";

	private readonly IDtacCrashLogger _crashLogger;

	private HakoPageState _currentState = new();

	private string? _workName;
	private string? _workSpaceName;

	public HakoPageState CurrentState => _currentState;

	public event EventHandler<HakoStateChangedEventArgs>? StateChanged;

	public HakoPresenter(IDtacCrashLogger crashLogger)
	{
		_crashLogger = crashLogger ?? throw new ArgumentNullException(nameof(crashLogger));
	}

	// ---------- Intents from View ----------

	/// <summary>
	/// Called when the AffectDate property changes.
	/// Updates <see cref="HakoPageState.AffectDateText"/> and raises StateChanged.
	/// </summary>
	public void OnAffectDateChanged(string? newValue)
	{
		_currentState.AffectDateText = AffectDateLabelTextPrefix + newValue;
		RaiseStateChanged(HakoStateSection.AffectDate);
	}

	/// <summary>
	/// Called when the WorkName property changes.
	/// Updates <see cref="HakoPageState.WorkInfoText"/> and raises StateChanged.
	/// </summary>
	public void OnWorkNameChanged(string? newValue)
	{
		_workName = newValue;
		UpdateWorkInfoText();
	}

	/// <summary>
	/// Called when the WorkSpaceName property changes.
	/// Updates <see cref="HakoPageState.WorkInfoText"/> and raises StateChanged.
	/// </summary>
	public void OnWorkSpaceNameChanged(string? newValue)
	{
		_workSpaceName = newValue;
		UpdateWorkInfoText();
	}

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

	private void UpdateWorkInfoText()
	{
		_currentState.WorkInfoText = $"{_workName}\n{_workSpaceName}";
		RaiseStateChanged(HakoStateSection.WorkInfo);
	}

	private void RaiseStateChanged(HakoStateSection changed)
	{
		StateChanged?.Invoke(this, new HakoStateChangedEventArgs(changed));
	}
}
