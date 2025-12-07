using TRViS.Services;

namespace TRViS;

public static partial class Util
{
	private static readonly NLog.Logger logger;

	static Util()
	{
		logger = LoggerService.GetGeneralLogger();
	}
}
