using TRViS.ViewModels;

namespace TRViS;

public partial class EasterEggPage : ContentPage
{
	EasterEggPageViewModel viewModel { get; }

	public EasterEggPage(EasterEggPageViewModel vm)
	{
		InitializeComponent();

		viewModel = vm;

		BindingContext = vm;
	}
}
