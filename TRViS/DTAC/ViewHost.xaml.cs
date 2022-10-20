using System.ComponentModel;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class ViewHost : ContentPage
{
	DTACViewHostViewModel ViewModel { get; }

	public ViewHost(AppViewModel vm)
	{
		InitializeComponent();

		ViewModel = new(vm);
		BindingContext = ViewModel;

		ViewModel.PropertyChanged += ViewModel_PropertyChanged;

		VerticalStylePageView.SetBinding(VerticalStylePage.SelectedTrainDataProperty, new Binding()
		{
			Source = vm,
			Path = nameof(AppViewModel.SelectedTrainData)
		});

		UpdateContent();
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DTACViewHostViewModel.TabMode))
			UpdateContent();
	}

	void UpdateContent()
	{
		HakoView.IsVisible = ViewModel.IsHakoMode;
		VerticalStylePageView.IsVisible = ViewModel.IsVerticalViewMode;
		WorkAffixView.IsVisible = ViewModel.IsWorkAffixMode;
	}
}
