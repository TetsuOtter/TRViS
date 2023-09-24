namespace TRViS;

public class AppThemeGenericsBindingExtension<T> : AppThemeBindingExtension where T : class
{
	private T _Dark;
	private T _Default;
	public new T Dark => base.Dark as T ?? _Dark;
	public new T Light => base.Light as T ?? _Default;
	public new T Default => base.Default as T ?? _Default;
	public new T Value => base.Value as T ?? _Default;

	public AppThemeGenericsBindingExtension(T Default, T Dark)
	{
		base.Default
			= base.Light
			= _Default
			= Default
			;

		base.Dark = _Dark = Dark;
	}

	public virtual void Apply(BindableObject? elem, BindableProperty prop)
		=> elem?.SetAppTheme(prop, this.Light, this.Dark);
}

public class AppThemeColorBindingExtension : AppThemeGenericsBindingExtension<Color>
{
	public AppThemeColorBindingExtension(Color Default, Color Dark) : base(Default, Dark) { }

	public override void Apply(BindableObject? elem, BindableProperty prop)
		=> elem?.SetAppThemeColor(prop, this.Light, this.Dark);
}
