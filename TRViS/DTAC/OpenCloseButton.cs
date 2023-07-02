using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<string>("TextWhenOpen")]
[DependencyProperty<string>("TextWhenClosed")]
public partial class OpenCloseButton : Button
{
	public event EventHandler<ValueChangedEventArgs<bool>>? IsOpenChanged;

	public OpenCloseButton()
	{
		this.SetBinding(TextProperty, new Binding()
		{
			Source = this,
			Path = nameof(TextWhenClosed)
		});

		DataTrigger dataTrigger = new(typeof(OpenCloseButton))
		{
			Binding = new Binding()
			{
				Source = this,
				Path = nameof(IsOpen)
			},
			Value = true,
		};

		dataTrigger.Setters.Add(new()
		{
			Property = TextProperty,
			Value = new Binding()
			{
				Source = this,
				Path = nameof(TextWhenOpen)
			}
		});

		Triggers.Add(dataTrigger);

		CornerRadius = 4;
		Padding = 0;
		BorderWidth = 0;
		FontFamily = "MaterialIconsRegular";
		FontSize = 40;
		DTACElementStyles.OpenCloseButtonBGColor.Apply(this, BackgroundColorProperty);
		DTACElementStyles.OpenCloseButtonTextColor.Apply(this, TextColorProperty);

		Shadow = new()
		{
			Brush = Colors.Black,
			Offset = new(1, 1),
			Radius = 2,
			Opacity = 0.4f
		};

		Clicked += (_, _) => IsOpen = !IsOpen;
	}

	partial void OnIsOpenChanged(bool oldValue, bool newValue)
		=> IsOpenChanged?.Invoke(this, new(oldValue, newValue));
}
