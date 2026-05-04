namespace TRViS.DTAC.Logic;

using TRViS.IO.Models.DB;

/// <summary>
/// Singleton service for managing shared ViewHost state across the application.
/// This is the single source of truth for selected work group, work, and train.
/// </summary>
public class SharedViewHostState
{
	private static SharedViewHostState? _instance;
	private static readonly object _lock = new object();

	private ViewHostState _state = new();
	private TrainData? _selectedTrainData;
	private Work? _selectedWork;
	private WorkGroup? _selectedWorkGroup;

	/// <summary>
	/// Fired when the state changes
	/// </summary>
	public event EventHandler? StateChanged;

	private SharedViewHostState()
	{
	}

	/// <summary>
	/// Gets the singleton instance
	/// </summary>
	public static SharedViewHostState Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_lock)
				{
					if (_instance == null)
					{
						_instance = new SharedViewHostState();
					}
				}
			}
			return _instance;
		}
	}

	/// <summary>
	/// Gets the current ViewHostState
	/// </summary>
	public ViewHostState State => _state;

	/// <summary>
	/// Gets or sets the selected work group
	/// </summary>
	public WorkGroup? SelectedWorkGroup
	{
		get => _selectedWorkGroup;
		set
		{
			if (!AreWorkGroupsEqual(_selectedWorkGroup, value))
			{
				_selectedWorkGroup = value;
				UpdateSelectedWorkGroup(value?.Name);
			}
		}
	}

	/// <summary>
	/// Gets or sets the selected work
	/// </summary>
	public Work? SelectedWork
	{
		get => _selectedWork;
		set
		{
			if (!AreWorksEqual(_selectedWork, value))
			{
				_selectedWork = value;
				UpdateSelectedWork(value?.Name);
			}
		}
	}

	/// <summary>
	/// Gets or sets the selected train data
	/// </summary>
	public TrainData? SelectedTrainData
	{
		get => _selectedTrainData;
		set
		{
			if (!AreTrainDataEqual(_selectedTrainData, value))
			{
				_selectedTrainData = value;
				UpdateSelectedTrain(value);
			}
		}
	}

	/// <summary>
	/// Updates the state when work group selection changes
	/// </summary>
	private void UpdateSelectedWorkGroup(string? workGroupName)
	{
		ViewHostStateFactory.UpdateSelectedWorkGroup(_state, workGroupName);
		RaiseStateChanged();
	}

	/// <summary>
	/// Updates the state when work selection changes
	/// </summary>
	private void UpdateSelectedWork(string? workName)
	{
		ViewHostStateFactory.UpdateSelectedWork(_state, workName);
		RaiseStateChanged();
	}

	/// <summary>
	/// Updates the state when train selection changes
	/// </summary>
	private void UpdateSelectedTrain(TrainData? trainData)
	{
		if (trainData != null && _selectedWork != null)
		{
			// Parse the work's AffectDate string to DateTime
			DateTime? affectDateTime = null;
			if (!string.IsNullOrEmpty(_selectedWork.AffectDate))
			{
				if (DateTime.TryParse(_selectedWork.AffectDate, out var parsed))
				{
					affectDateTime = parsed;
				}
			}

			string affectDate = ViewHostStateFactory.FormatAffectDate(
				affectDateTime,
				trainData.DayCount ?? 0
			);
			ViewHostStateFactory.UpdateSelectedTrain(_state, affectDate, trainData.DayCount ?? 0);
		}
		RaiseStateChanged();
	}

	/// <summary>
	/// Marks work group as processed (no longer changed)
	/// </summary>
	public void MarkWorkGroupProcessed()
	{
		ViewHostStateFactory.MarkWorkGroupProcessed(_state);
		RaiseStateChanged();
	}

	/// <summary>
	/// Marks work as processed (no longer changed)
	/// </summary>
	public void MarkWorkProcessed()
	{
		ViewHostStateFactory.MarkWorkProcessed(_state);
		RaiseStateChanged();
	}

	/// <summary>
	/// Marks train as processed (no longer changed)
	/// </summary>
	public void MarkTrainProcessed()
	{
		ViewHostStateFactory.MarkTrainProcessed(_state);
		RaiseStateChanged();
	}

	/// <summary>
	/// Checks if two work groups are equal by ID
	/// </summary>
	private static bool AreWorkGroupsEqual(WorkGroup? a, WorkGroup? b)
	{
		if (a is null && b is null)
			return true;
		if (a is null || b is null)
			return false;
		return a.Id == b.Id;
	}

	/// <summary>
	/// Checks if two works are equal by ID
	/// </summary>
	private static bool AreWorksEqual(Work? a, Work? b)
	{
		if (a is null && b is null)
			return true;
		if (a is null || b is null)
			return false;
		return a.Id == b.Id;
	}

	/// <summary>
	/// Checks if two train data are equal by ID
	/// </summary>
	private static bool AreTrainDataEqual(TrainData? a, TrainData? b)
	{
		if (a is null && b is null)
			return true;
		if (a is null || b is null)
			return false;
		return a.Id == b.Id;
	}

	/// <summary>
	/// Raises the StateChanged event
	/// </summary>
	private void RaiseStateChanged()
	{
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Resets the state to empty
	/// </summary>
	public void Reset()
	{
		_state = new();
		_selectedTrainData = null;
		_selectedWork = null;
		_selectedWorkGroup = null;
		RaiseStateChanged();
	}
}
