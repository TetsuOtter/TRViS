using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class VerticalStylePage : ContentPage
{
	public VerticalStylePage(AppViewModel viewModel)
	{
		InitializeComponent();

		BindingContext = viewModel;

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
			Content = new ScrollView()
			{
				Content = this.Content
			};
	}
}
