namespace TRViS;

public static partial class Utils
{
  public static string ThicknessToString(in Thickness thickness)
  {
    return $"({thickness.Left},{thickness.Top},{thickness.Right},{thickness.Bottom})";
  }
}
