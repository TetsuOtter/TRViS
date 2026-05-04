namespace TRViS.DTAC.Logic;

/// <summary>
/// Represents the complete state of the ViewHost (DTAC main view).
/// This model encapsulates all display-related state for the DTAC view host.
/// </summary>
public class ViewHostState
{
  /// <summary>
  /// Information about the selected work group
  /// </summary>
  public WorkGroupInfo SelectedWorkGroup { get; set; } = new();

  /// <summary>
  /// Information about the selected work
  /// </summary>
  public WorkInfo SelectedWork { get; set; } = new();

  /// <summary>
  /// Information about the selected train
  /// </summary>
  public TrainInfo SelectedTrain { get; set; } = new();
}

/// <summary>
/// Represents work group information
/// </summary>
public class WorkGroupInfo
{
  /// <summary>
  /// The name of the work group
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Whether the work group was changed
  /// </summary>
  public bool IsChanged { get; set; } = false;
}

/// <summary>
/// Represents work information
/// </summary>
public class WorkInfo
{
  /// <summary>
  /// The name of the work
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Whether the work was changed
  /// </summary>
  public bool IsChanged { get; set; } = false;
}

/// <summary>
/// Represents train information for the ViewHost
/// </summary>
public class TrainInfo
{
  /// <summary>
  /// The affect date string (formatted)
  /// </summary>
  public string AffectDate { get; set; } = string.Empty;

  /// <summary>
  /// The day count for the train
  /// </summary>
  public int DayCount { get; set; } = 0;

  /// <summary>
  /// Whether the train was changed
  /// </summary>
  public bool IsChanged { get; set; } = false;
}
