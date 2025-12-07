using TRViS.Services;

namespace TRViS.Utils;

public static partial class Util
{
	private static readonly NLog.Logger logger;

	static Util()
	{
		logger = LoggerService.GetGeneralLogger();
	}
}
