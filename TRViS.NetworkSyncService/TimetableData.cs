namespace TRViS.NetworkSyncService;

/// <summary>
/// 時刻表データの変更スコープを示します
/// </summary>
public enum TimetableScopeType
{
  /// <summary>WorkGroup単位での変更</summary>
  WorkGroup,
  /// <summary>Work単位での変更</summary>
  Work,
  /// <summary>Train単位での変更</summary>
  Train
}

/// <summary>
/// サーバーから受け取る時刻表データ
/// </summary>
public class TimetableData
{
  /// <summary>
  /// 時刻表の対象となるWorkGroupId
  /// </summary>
  public string? WorkGroupId { get; set; }

  /// <summary>
  /// 時刻表の対象となるWorkId
  /// </summary>
  public string? WorkId { get; set; }

  /// <summary>
  /// 時刻表の対象となるTrainId
  /// </summary>
  public string? TrainId { get; set; }

  /// <summary>
  /// 時刻表データの変更スコープ
  /// </summary>
  public TimetableScopeType Scope { get; set; }

  /// <summary>
  /// 実際の時刻表JSONデータ
  /// </summary>
  public string JsonData { get; set; } = string.Empty;
}
