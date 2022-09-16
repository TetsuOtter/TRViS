using TRViS.ViewModels;

namespace TRViS;

public partial class SelectTrainPage : ContentPage
{
	AppViewModel viewModel { get; }

	public SelectTrainPage(AppViewModel viewModel)
	{
		InitializeComponent();

		this.viewModel = viewModel;
		this.BindingContext = viewModel;
	}

	async void Button_Clicked(object sender, EventArgs e)
	{
		try
		{
			var result = await FilePicker.Default.PickAsync();

			if (result is not null)
			{
				viewModel.Loader?.Dispose();

				viewModel.Loader = new(result.FullPath);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Cannot Open File", ex.ToString(), "OK");
		}
	}
}
