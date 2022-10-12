using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<string>("TextWhenOpen")]
[DependencyProperty<string>("TextWhenClosed")]
public partial class OpenCloseButton : Button
{
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
		BackgroundColor = Colors.White;
		BorderWidth = 0;
		TextColor = new(0xAA, 0xAA, 0xAA);
		FontFamily = "MaterialIconsRegular";
		FontSize = 40;

		Shadow = new()
		{
			Brush = Colors.Black,
			Offset = new(1, 1),
			Radius = 2,
			Opacity = 0.4f
		};

		Clicked += (_, _) => IsOpen = !IsOpen;
	}
}
