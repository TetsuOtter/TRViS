using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen")]
[DependencyProperty<bool>("IsRunning")]
[DependencyProperty<bool>("IsLocationServiceEnabled")]
[DependencyProperty<bool>("CanUseLocationService")]
public partial class PageHeader : Grid
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	static readonly ColumnDefinitionCollection DefaultColumnDefinitions = new()
	{
		new ColumnDefinition(new GridLength(1, GridUnitType.Star)),

		// under total: 378
		new ColumnDefinition(186),
		new ColumnDefinition(128),
		new ColumnDefinition(60),
	};

	#region Affect Date Label

	readonly Label AffectDateLabel = DTACElementStyles.AffectDateLabelStyle<Label>();

	string _AffectDateLabelText = "";
	public string AffectDateLabelText
	{
		get => _AffectDateLabelText;
		set
		{
			if (_AffectDateLabelText == value)
			{
				logger.Trace("newValue: {0} (unchanged)", value);
				return;
			}

			_AffectDateLabelText = value;

			AffectDateLabel.Text = DTACElementStyles.AffectDateLabelTextPrefix + value;
			logger.Info("AffectDateLabelText: {0}", value);
		}
	}
	#endregion

	#region Start / End Run Button
	readonly StartEndRunButton StartEndRunButton = new();

	public event EventHandler<ValueChangedEventArgs<bool>>? IsRunningChanged
	{
		add => StartEndRunButton.IsCheckedChanged += value;
		remove => StartEndRunButton.IsCheckedChanged -= value;
	}

	partial void OnIsRunningChanged(bool newValue)
	{
		logger.Info("IsRunning: {0}", newValue);
		StartEndRunButton.IsChecked = newValue;

		LocationServiceButton.IsEnabled = CanUseLocationService && newValue;
	}

	private void StartEndRunButton_IsCheckedChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Trace("newValue: {0}", e.NewValue);

		this.IsRunning = e.NewValue;

		LocationServiceButton.IsEnabled = CanUseLocationService && e.NewValue;
	}
	#endregion

	#region Location Service Button
	readonly LocationServiceButton LocationServiceButton = new();

	public event EventHandler<ValueChangedEventArgs<bool>>? IsLocationServiceEnabledChanged
	{
		add => LocationServiceButton.IsCheckedChanged += value;
		remove => LocationServiceButton.IsCheckedChanged -= value;
	}

	partial void OnIsLocationServiceEnabledChanged(bool newValue)
	{
		logger.Info("IsLocationServiceEnabled: {0}", newValue);
		LocationServiceButton.IsChecked = newValue;
	}

	private void LocationServiceButton_IsCheckedChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Trace("newValue: {0}", e.NewValue);
		IsLocationServiceEnabled = e.NewValue;
	}
	#endregion

	#region Open / Close Button
	readonly OpenCloseButton OpenCloseButton = new();

	public event EventHandler<ValueChangedEventArgs<bool>>? IsOpenChanged
	{
		add => OpenCloseButton.IsOpenChanged += value;
		remove => OpenCloseButton.IsOpenChanged -= value;
	}

	partial void OnIsOpenChanged(bool newValue)
	{
		logger.Info("OpenCloseButton.IsOpen: {0}", newValue);
		OpenCloseButton.IsOpen = newValue;
	} 

	private void OpenCloseButton_IsOpenChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Trace("newValue: {0}", e.NewValue);
		IsOpen = e.NewValue;
	}
	#endregion

	public PageHeader()
	{
		logger.Trace("Creating...");

		ColumnDefinitions = DefaultColumnDefinitions;

		StartEndRunButton.VerticalOptions = LayoutOptions.Center;
		StartEndRunButton.HorizontalOptions = LayoutOptions.End;
		StartEndRunButton.Margin = new(2);
		StartEndRunButton.IsCheckedChanged += (_, e) => this.IsRunning = e.NewValue;

		LocationServiceButton.IsEnabled = false;
		LocationServiceButton.Margin = new(4, 8);
		LocationServiceButton.IsCheckedChanged += LocationServiceButton_IsCheckedChanged;

		OpenCloseButton.TextWhenOpen = "\xe5ce";
		OpenCloseButton.TextWhenClosed = "\xe5cf";
		OpenCloseButton.IsOpenChanged += OpenCloseButton_IsOpenChanged;
		OpenCloseButton.HorizontalOptions = LayoutOptions.Center;
		OpenCloseButton.VerticalOptions = LayoutOptions.Center;

		this.Add(
			AffectDateLabel,
			column: 0
		);
		this.Add(StartEndRunButton,
			column: 1
		);
		this.Add(LocationServiceButton,
			column: 2
		);
		this.Add(
			OpenCloseButton,
			column: 3
		);

		logger.Trace("Created");
	}
}
