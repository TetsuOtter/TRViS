using TRViS.IO;
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

		viewModel.Loader ??= new SampleDataLoader();
	}

	async void Button_Clicked(object sender, EventArgs e)
	{
		try
		{
			var result = await FilePicker.Default.PickAsync();

			if (result is not null)
			{
				viewModel.Loader?.Dispose();

				if (result.FullPath.EndsWith(".json"))
					viewModel.Loader = await LoaderJson.InitFromFileAsync(result.FullPath);
				else
					viewModel.Loader = new LoaderSQL(result.FullPath);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Cannot Open File", ex.ToString(), "OK");
		}
	}
}
