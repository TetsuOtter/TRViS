using TRViS.Controls;

namespace TRViS;

public class AppThemeGenericsBindingExtension<T> : AppThemeBindingExtension where T : class
{
	private readonly T _Dark;
	private readonly T _Default;
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
	{
		elem?.SetAppTheme(prop, this.Light, this.Dark);
		if (elem is HtmlAutoDetectLabel label && prop == HtmlAutoDetectLabel.TextColorProperty)
			label.CurrentAppThemeColorBindingExtension = this as AppThemeColorBindingExtension;
	}
}

public class AppThemeGenericsValueTypeBindingExtension<T> : AppThemeBindingExtension
{
	public AppThemeGenericsValueTypeBindingExtension(T Default, T Dark)
	{
		base.Default
			= base.Light
			= Default
			;

		base.Dark = Dark;
	}

	public virtual void Apply(BindableObject? elem, BindableProperty prop)
	{
		elem?.SetAppTheme(prop, this.Light, this.Dark);
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
	
	public AppThemeGenericsBindingExtension<Brush> ToBrushTheme()
		=> new(new SolidColorBrush(this.Default), new SolidColorBrush(this.Dark));
}
