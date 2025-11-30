using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;
using TR.Maui.AnchorPopover;

namespace TRViS.DTAC;

public partial class QuickSwitchPopup : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private IAnchorPopover? _popover;
	private AppViewModel ViewModel { get; }

	private bool _isWorkGroupTabSelected = true;
	private bool IsWorkGroupTabSelected
	{
		get => _isWorkGroupTabSelected;
		set
		{
			if (_isWorkGroupTabSelected == value)
				return;
			_isWorkGroupTabSelected = value;
			UpdateTabStyles();
		}
	}

	public QuickSwitchPopup()
	{
		logger.Trace("Creating...");

		ViewModel = InstanceManager.AppViewModel;

		InitializeComponent();

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		// Set up lists
		WorkGroupListView.ItemsSource = ViewModel.WorkGroupList;
		WorkGroupListView.SelectedItem = ViewModel.SelectedWorkGroup;
		WorkListView.ItemsSource = ViewModel.WorkList;
		WorkListView.SelectedItem = ViewModel.SelectedWork;

		// Apply styles
		DTACElementStyles.TabAreaBGColor.Apply(WorkGroupListContainer, Border.BackgroundColorProperty);
		DTACElementStyles.TabAreaBGColor.Apply(WorkListContainer, Border.BackgroundColorProperty);
		DTACElementStyles.TimetableTextColor.Apply(WorkGroupTabLabel, Label.TextColorProperty);
		DTACElementStyles.TimetableTextColor.Apply(WorkTabLabel, Label.TextColorProperty);

		// Initial tab selection is WorkGroup
		IsWorkGroupTabSelected = true;
		UpdateTabStyles();

		logger.Trace("Created");
	}

	private void UpdateTabStyles()
	{
		logger.Trace("IsWorkGroupTabSelected: {0}", IsWorkGroupTabSelected);

		// Update tab button backgrounds
		if (IsWorkGroupTabSelected)
		{
			DTACElementStyles.DefaultBGColor.Apply(WorkGroupTabButton, Border.BackgroundColorProperty);
			DTACElementStyles.TabButtonBGColor.Apply(WorkTabButton, Border.BackgroundColorProperty);
		}
		else
		{
			DTACElementStyles.TabButtonBGColor.Apply(WorkGroupTabButton, Border.BackgroundColorProperty);
			DTACElementStyles.DefaultBGColor.Apply(WorkTabButton, Border.BackgroundColorProperty);
		}

		// Update list visibility
		WorkGroupListContainer.IsVisible = IsWorkGroupTabSelected;
		WorkListContainer.IsVisible = !IsWorkGroupTabSelected;
	}

	private void WorkGroupTab_Tapped(object sender, EventArgs e)
	{
		logger.Info("WorkGroup tab tapped");
		IsWorkGroupTabSelected = true;
	}

	private void WorkTab_Tapped(object sender, EventArgs e)
	{
		logger.Info("Work tab tapped");
		IsWorkGroupTabSelected = false;
	}

	private void WorkGroupListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
	{
		if (e.SelectedItem is WorkGroup selectedWorkGroup)
		{
			logger.Info("WorkGroup selected: {0}", selectedWorkGroup.Name);
			ViewModel.SelectedWorkGroup = selectedWorkGroup;

			// Update Work list with new WorkGroup's works
			WorkListView.ItemsSource = ViewModel.WorkList;
			WorkListView.SelectedItem = ViewModel.SelectedWork;

			// Automatically switch to Work tab
			IsWorkGroupTabSelected = false;
		}
	}

	private void WorkListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
	{
		if (e.SelectedItem is Work selectedWork)
		{
			logger.Info("Work selected: {0}", selectedWork.Name);
			ViewModel.SelectedWork = selectedWork;
		}
	}

	internal void SetPopover(IAnchorPopover popover)
	{
		_popover = popover;
	}
}
