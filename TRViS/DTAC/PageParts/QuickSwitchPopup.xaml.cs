using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class QuickSwitchPopup : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
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

		// Set up tab buttons
		WorkGroupTabButton.Tapped += WorkGroupTab_Tapped;
		WorkTabButton.Tapped += WorkTab_Tapped;

		// Initial tab selection is WorkGroup
		IsWorkGroupTabSelected = true;
		UpdateTabStyles();

		logger.Trace("Created");
	}

	private void UpdateTabStyles()
	{
		logger.Trace("IsWorkGroupTabSelected: {0}", IsWorkGroupTabSelected);

		WorkGroupTabButton.IsSelected = IsWorkGroupTabSelected;
		WorkTabButton.IsSelected = !IsWorkGroupTabSelected;

		// Update list visibility
		WorkGroupListContainer.IsVisible = IsWorkGroupTabSelected;
		WorkListContainer.IsVisible = !IsWorkGroupTabSelected;
	}

	private void WorkGroupTab_Tapped(object? sender, EventArgs e)
	{
		logger.Info("WorkGroup tab tapped");
		IsWorkGroupTabSelected = true;
	}

	private void WorkTab_Tapped(object? sender, EventArgs e)
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
}
