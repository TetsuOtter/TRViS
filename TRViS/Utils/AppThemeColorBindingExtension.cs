using TRViS.Controls;

namespace TRViS;

public class AppThemeGenericsBindingExtension<T> : AppThemeBindingExtension where T : class
{
	public new T? Dark => base.Dark as T;
	public new T? Light => base.Light as T;
	public new T? Default => base.Default as T;
	public new T? Value => base.Value as T;

	public AppThemeGenericsBindingExtension(T Default, T Dark)
	{
		base.Default = Default;
		base.Light = Default;
		base.Dark = Dark;
	}

	public virtual void Apply(BindableObject? elem, BindableProperty prop)
	{
		elem?.SetAppTheme(prop, this.Light, this.Dark);
		if (elem is HtmlAutoDetectLabel label && prop == HtmlAutoDetectLabel.TextColorProperty)
			label.CurrentAppThemeColorBindingExtension = this as AppThemeColorBindingExtension;
	}
}

public class AppThemeColorBindingExtension : AppThemeGenericsBindingExtension<Color>
{
	public AppThemeColorBindingExtension(Color Default, Color Dark) : base(Default, Dark) { }

	public override void Apply(BindableObject? elem, BindableProperty prop)
	{
		elem?.SetAppThemeColor(prop, this.Light, this.Dark);
		if (elem is HtmlAutoDetectLabel label && prop == HtmlAutoDetectLabel.TextColorProperty)
			label.CurrentAppThemeColorBindingExtension = this;
	}
}
