using CommunityToolkit.Maui.Views;
using DependencyPropertyGenerator;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class SelectMarkerPopup : Popup
{
	public SelectMarkerPopup()
	{
		InitializeComponent();
	}

	public SelectMarkerPopup(DTACMarkerViewModel vm) : this()
	{
		BindingContext = vm;
	}
}
