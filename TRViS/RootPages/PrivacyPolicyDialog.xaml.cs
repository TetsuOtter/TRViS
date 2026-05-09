using TRViS.FirebaseWrapper;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class PrivacyPolicyDialog : ContentPage
{
	// Mirrors the StartHome page's threshold — on landscape phones the body
	// has too little vertical room for a 4-row stack, so we switch to a
	// 2-column layout (markdown left, description+settings right).
	const double PHONE_SHORT_SIDE_MAX = 500;

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	FirebaseSettingViewModel FirebaseSettingViewModel { get; }

	bool _isLandscapePhone;

	public PrivacyPolicyDialog()
	{
		logger.Trace("Creating");
		// Work on a working copy so cancellation (close without save) leaves the live VM untouched.
		FirebaseSettingViewModel = new(InstanceManager.FirebaseSettingViewModel);
		BindingContext = FirebaseSettingViewModel;
		InitializeComponent();
		SizeChanged += (_, __) => ApplyOrientationLayout();
		logger.Trace("Created");
	}

	void ApplyOrientationLayout()
	{
		if (Width <= 0 || Height <= 0)
			return;
		bool isLandscapePhone = Width > Height && Math.Min(Width, Height) < PHONE_SHORT_SIDE_MAX;
		// Idempotent guard: no need to reshuffle when the orientation hasn't
		// changed AND the grid has been initialised at least once. The first
		// call always populates RowDefinitions, so subsequent same-orientation
		// SizeChanged events (caused by minor pixel drift during animations)
		// are short-circuited.
		if (isLandscapePhone == _isLandscapePhone && RootGrid.RowDefinitions.Count > 0)
			return;
		_isLandscapePhone = isLandscapePhone;
		RootGrid.RowDefinitions.Clear();
		RootGrid.ColumnDefinitions.Clear();
		if (isLandscapePhone)
		{
			// 2 rows × 2 columns. Header is full-width across columns; the
			// markdown fills the left body cell. The right body cell hosts
			// `RightSection`, a sub-grid (*,Auto) into which we reparent
			// DescScroll (description in a ScrollView) and SettingsBorder.
			// The ScrollView keeps the description bounded by the available
			// height instead of overflowing into the bottom-anchored
			// settings card on short screens (e.g. iPhone SE landscape).
			MoveToRightSection();
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
			RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			Grid.SetRow(HeaderRow, 0); Grid.SetRowSpan(HeaderRow, 1);
			Grid.SetColumn(HeaderRow, 0); Grid.SetColumnSpan(HeaderRow, 2);
			Grid.SetRow(MarkdownBorder, 1); Grid.SetRowSpan(MarkdownBorder, 1);
			Grid.SetColumn(MarkdownBorder, 0); Grid.SetColumnSpan(MarkdownBorder, 1);
			Grid.SetRow(RightSection, 1); Grid.SetRowSpan(RightSection, 1);
			Grid.SetColumn(RightSection, 1); Grid.SetColumnSpan(RightSection, 1);
		}
		else
		{
			// Portrait / tablet: original 4-row stack — header, description,
			// scrollable markdown, settings.
			MoveBackFromRightSection();
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			Grid.SetRow(HeaderRow, 0); Grid.SetRowSpan(HeaderRow, 1);
			Grid.SetColumn(HeaderRow, 0); Grid.SetColumnSpan(HeaderRow, 1);
			Grid.SetRow(DescLabel, 1); Grid.SetRowSpan(DescLabel, 1);
			Grid.SetColumn(DescLabel, 0); Grid.SetColumnSpan(DescLabel, 1);
			Grid.SetRow(MarkdownBorder, 2); Grid.SetRowSpan(MarkdownBorder, 1);
			Grid.SetColumn(MarkdownBorder, 0); Grid.SetColumnSpan(MarkdownBorder, 1);
			Grid.SetRow(SettingsBorder, 3); Grid.SetRowSpan(SettingsBorder, 1);
			Grid.SetColumn(SettingsBorder, 0); Grid.SetColumnSpan(SettingsBorder, 1);
		}
	}

	// Reparent DescLabel + SettingsBorder out of RootGrid and into the
	// landscape-only RightSection sub-grid. DescLabel is wrapped in DescScroll
	// so its height stays bounded by its row, allowing scrolling instead of
	// overflowing onto the SettingsBorder below.
	void MoveToRightSection()
	{
		if (RootGrid.Children.Contains(DescLabel))
			RootGrid.Children.Remove(DescLabel);
		if (RootGrid.Children.Contains(SettingsBorder))
			RootGrid.Children.Remove(SettingsBorder);

		if (DescScroll.Content != DescLabel)
			DescScroll.Content = DescLabel;

		if (!RightSection.Children.Contains(SettingsBorder))
		{
			Grid.SetRow(SettingsBorder, 1);
			Grid.SetColumn(SettingsBorder, 0);
			Grid.SetRowSpan(SettingsBorder, 1);
			Grid.SetColumnSpan(SettingsBorder, 1);
			RightSection.Children.Add(SettingsBorder);
		}
		RightSection.IsVisible = true;
	}

	// Inverse of MoveToRightSection — return DescLabel and SettingsBorder
	// to RootGrid for the portrait/tablet stack layout.
	void MoveBackFromRightSection()
	{
		RightSection.IsVisible = false;
		if (RightSection.Children.Contains(SettingsBorder))
			RightSection.Children.Remove(SettingsBorder);
		if (DescScroll.Content == DescLabel)
			DescScroll.Content = null;

		if (!RootGrid.Children.Contains(DescLabel))
			RootGrid.Children.Add(DescLabel);
		if (!RootGrid.Children.Contains(SettingsBorder))
			RootGrid.Children.Add(SettingsBorder);
	}

	private void OnResetButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Reset clicked");
		FirebaseSettingViewModel.CopyFrom(InstanceManager.FirebaseSettingViewModel);
		FirebaseSettingViewModel.IsEnabled = false;
		FirebaseSettingViewModel.SaveAndApplySettings(true);
	}

	private async void OnSaveButtonClicked(object? sender, EventArgs e)
	{
		logger.Trace("Save clicked");
		FirebaseSettingViewModel.IsEnabled = true;
		FirebaseSettingViewModel.LastAcceptedPrivacyPolicyRevision = Constants.PRIVACY_POLICY_REVISION;

		InstanceManager.FirebaseSettingViewModel.CopyFrom(FirebaseSettingViewModel);
		InstanceManager.FirebaseSettingViewModel.SaveAndApplySettings(true);

		MauiProgram.ConfigureFirebase();
		InstanceManager.AnalyticsWrapper.Log(AnalyticsEvents.PrivacyPolicyAccepted);

		await Navigation.PopModalAsync();
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		logger.Trace("Close clicked");
		await Navigation.PopModalAsync();
	}
}
