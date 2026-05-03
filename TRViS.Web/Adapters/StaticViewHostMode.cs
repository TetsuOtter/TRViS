using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;

namespace TRViS.Web.Adapters;

/// <summary>
/// In Blazor we render the Vertical timetable directly, so the view host is always
/// "visible" and in vertical mode.
/// </summary>
public sealed class StaticViewHostMode : IViewHostModeProvider
{
	public bool IsViewHostVisible => true;
	public bool IsVerticalViewMode => true;
	public DTACTabMode TabMode => DTACTabMode.VerticalView;

	public event PropertyChangedEventHandler? PropertyChanged
	{
		add { }
		remove { }
	}
}
