namespace TRViS;

public static partial class Utils
{
	private static readonly NLog.Logger logger;

  static Utils()
  {
    logger = NLog.LogManager.GetCurrentClassLogger();
  }
}
