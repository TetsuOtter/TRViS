namespace TRViS.Utils;

public static partial class Util
{
	public static string ThicknessToString(in Thickness thickness)
	{
		return $"({thickness.Left},{thickness.Top},{thickness.Right},{thickness.Bottom})";
	}
}
