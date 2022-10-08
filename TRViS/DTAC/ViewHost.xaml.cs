using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class ViewHost : ContentPage
{
	public ViewHost(AppViewModel vm)
	{
		InitializeComponent();

		BindingContext = new DTACViewHostViewModel(vm);
	}
}
