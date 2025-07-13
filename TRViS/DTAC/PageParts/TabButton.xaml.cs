using System.ComponentModel;
using System.Runtime.CompilerServices;

using DependencyPropertyGenerator;

using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<DTACViewHostViewModel.Mode>("CurrentMode", DefaultBindingMode = DefaultBindingMode.TwoWay)]
[DependencyProperty<DTACViewHostViewModel.Mode>("TargetMode")]
[DependencyProperty<bool>("IsSelected", IsReadOnly = true)]
[DependencyProperty<string>("Text")]
public partial class TabButton : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public static readonly Color BASE_COLOR_DISABLED = new(0xDD, 0xDD, 0xDD);

	public static readonly double NORMAL_MODE_WIDTH = 152;

	public TabButton()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		UpdateIsSelectedProperty();
		DTACElementStyles.Instance.TimetableTextColor.Apply(ButtonLabel, Label.TextColorProperty);

		InstanceManager.AppViewModel.PropertyChanged += AppViewModel_PropertyChanged;
		OnWindowWidthChanged(InstanceManager.AppViewModel.WindowWidth);

		OnIsEnabledChanged(IsEnabled);

		logger.Trace("Created");
	}

	private void AppViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(AppViewModel.WindowWidth):
				OnWindowWidthChanged(InstanceManager.AppViewModel.WindowWidth);
				break;
		}
	}
	protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);
		switch (propertyName)
		{
			case nameof(IsEnabled):
				OnIsEnabledChanged(IsEnabled);
				break;
		}
	}
	void OnWindowWidthChanged(double newValue)
	{
		if (newValue == 0)
		{
			return;
		}
		try
		{
			logger.Trace("newValue: {0}", newValue);

			int tabButtonCount = 3;
			double calcedMaxWidth = (newValue - 8) / tabButtonCount;
			double widthRequestValue = Math.Min(calcedMaxWidth, NORMAL_MODE_WIDTH);
			logger.Trace("OnWindowWidthChanged WidthRequest newValue: {0}", widthRequestValue);
			WidthRequest = widthRequestValue;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "TabButton.OnWindowWidthChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	partial void OnCurrentModeChanged()
	{
		logger.Trace("CurrentMode: {0}", CurrentMode);
		UpdateIsSelectedProperty();
	}
	partial void OnTargetModeChanged()
	{
		logger.Trace("TargetMode: {0}", TargetMode);
		UpdateIsSelectedProperty();
	}

	void UpdateIsSelectedProperty()
	{
		IsSelected = (CurrentMode == TargetMode);
		logger.Info("CurrentMode: {0}, TargetMode: {1}, IsSelected: {2}", CurrentMode, TargetMode, IsSelected);
	}

	partial void OnTextChanged(string? newValue)
	{
		logger.Trace("newValue: {0}", newValue);
		ButtonLabel.Text = newValue;
	}

	partial void OnIsSelectedChanged(bool newValue)
	{
		logger.Trace("newValue: {0}", newValue);
		BottomLine.IsVisible = newValue;

		if (newValue)
		{
			DTACElementStyles.Instance.DefaultBGColor.Apply(BaseBox, BoxView.ColorProperty);
			BaseBox.Shadow.Opacity = 0.2f;
			logger.Info("Tab `{0}` selected", Text);
		}
		else
		{
			DTACElementStyles.Instance.TabButtonBGColor.Apply(BaseBox, BoxView.ColorProperty);
			BaseBox.Shadow.Opacity = 0;
			logger.Info("Tab `{0}` unselected", Text);
		}
	}

	private void OnIsEnabledChanged(bool newValue)
	{
		if (newValue)
		{
			ButtonLabel.Opacity = 1;
		}
		else
		{
			ButtonLabel.Opacity = 0.5;
			if (IsSelected)
			{
				InstanceManager.DTACViewHostViewModel.TabMode = DTACViewHostViewModel.Mode.Hako;
			}
		}
	}

	void BaseBox_Tapped(object sender, EventArgs e)
	{
		try
		{
			if (IsSelected || !IsEnabled)
			{
				logger.Info("Tapped `{0}` but... IsSelected: {1}, IsEnabled: {2}", Text, IsSelected, IsEnabled);
				return;
			}

			logger.Info("Tapped `{0}`", Text);
			CurrentMode = TargetMode;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "TabButton.BaseBox_Tapped");
			Utils.ExitWithAlert(ex);
		}
	}
}
