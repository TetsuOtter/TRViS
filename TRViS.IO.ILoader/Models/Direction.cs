namespace TRViS.IO.Models;

public enum Direction
{
	/// <summary>
	/// Inbound direction (駅位置が小さくなる方向に向かう)
	/// </summary>
	Inbound,
	/// <summary>
	/// Outbound direction (駅位置が大きくなる方向に向かう)
	/// </summary>
	Outbound,
}

public static class DirectionExtensions
{
	public static Direction FromInt(int direction)
	{
		return direction switch
		{
			< 0 => Direction.Inbound,
			> 0 => Direction.Outbound,
			_ => throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be non-zero"),
		};
	}

	public static int ToInt(this Direction direction)
	{
		return direction switch
		{
			Direction.Inbound => -1,
			Direction.Outbound => 1,
			_ => throw new ArgumentOutOfRangeException(nameof(direction), "Invalid Direction value"),
		};
	}
}
