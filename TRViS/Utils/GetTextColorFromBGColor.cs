namespace TRViS;

public static partial class Util
{
	// ref: http://www.asahi-net.or.jp/~gx4s-kmgi/page04.html

	public static Color GetTextColorFromBGColor(Color v)
		=> GetTextColorFromBGColor((int)(v.Red * 0xFF), (int)(v.Green * 0xFF), (int)(v.Blue * 0xFF));

	public static Color GetTextColorFromBGColor(int Color_Red, int Color_Green, int Color_Blue)
		=> (((Color_Red * 299) + (Color_Green * 587) + (Color_Blue * 114)) / 1000) >= 128 ? Colors.Black : Colors.White;
}
