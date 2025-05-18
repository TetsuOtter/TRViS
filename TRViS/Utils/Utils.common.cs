using TRViS.Services;

namespace TRViS;

public static partial class Utils
{
	private static readonly NLog.Logger logger;

	static Utils()
	{
		logger = LoggerService.GetGeneralLogger();
	}
}
