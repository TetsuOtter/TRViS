using TRViS.Services;

namespace TRViS.Web.Adapters;

public sealed class RealTimeProvider : ITimeProvider
{
	private TimeProgressionRate _rate = TimeProgressionRate.Normal;

	public TimeProgressionRate ProgressionRate
	{
		get => _rate;
		set
		{
			if (_rate == value) return;
			_rate = value;
			ProgressionRateChanged?.Invoke(this, value);
		}
	}

	public int GetCurrentTimeSeconds()
	{
		var now = DateTime.Now;
		return now.Hour * 3600 + now.Minute * 60 + now.Second;
	}

	public event EventHandler<TimeProgressionRate>? ProgressionRateChanged;
}
