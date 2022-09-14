using TRViS.ViewModels;

namespace TRViS;

public partial class SelectTrainPage : ContentPage
{
	public SelectTrainPage(AppViewModel viewModel)
	{
		InitializeComponent();

		this.BindingContext = viewModel;
	}
}
