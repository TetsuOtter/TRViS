using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.Web.Adapters;

public sealed class SystemClock : IClock
{
	public DateTime UtcNow => DateTime.UtcNow;
}
