using CommunityToolkit.Mvvm.ComponentModel;

namespace TRViS.ViewModels;

public partial class EasterEggPageViewModel : ObservableObject
{
	[ObservableProperty]
	Color _ShellBackgroundColor = Colors.Black;

	[ObservableProperty]
	Color _ShellTitleTextColor = Colors.White;

	[ObservableProperty]
	int _Color_Red;
	[ObservableProperty]
	int _Color_Green;
	[ObservableProperty]
	int _Color_Blue;

	public EasterEggPageViewModel()
	{
		if (Preferences.Default.ContainsKey(nameof(ShellBackgroundColor)))
		{
			int value = Preferences.Default.Get<int>(nameof(ShellBackgroundColor), 0);

			_ShellBackgroundColor = Color.FromInt(value);

			_Color_Red = (int)(ShellBackgroundColor.Red * 255);
			_Color_Green = (int)(ShellBackgroundColor.Green * 255);
			_Color_Blue = (int)(ShellBackgroundColor.Blue * 255);

			SetTitleTextColor();
		}
	}

	partial void OnShellBackgroundColorChanged(Color value)
	{
		if (value is not null)
		{
			Preferences.Default.Set<int>(nameof(ShellBackgroundColor), value.ToInt());

			SetTitleTextColor();
		}
	}

	void SetTitleTextColor()
	{
		// ref: http://www.asahi-net.or.jp/~gx4s-kmgi/page04.html
		ShellTitleTextColor = Utils.GetTextColorFromBGColor(Color_Red, Color_Green, Color_Blue);
	}

	partial void OnColor_RedChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
	partial void OnColor_GreenChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
	partial void OnColor_BlueChanged(int value)
		=> ShellBackgroundColor = Color.FromRgb(Color_Red, Color_Green, Color_Blue);
}

