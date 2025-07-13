using DependencyPropertyGenerator;

using TRViS.DTAC.HakoParts;
using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<string>("AffectDate")]
[DependencyProperty<string>("WorkName")]
[DependencyProperty<string>("WorkSpaceName")]
public partial class Hako : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	readonly HeaderView headerView = new();
	readonly Image backgroundImage = DTACElementStyles.Instance.BackgroundAppIconImage();
	readonly ScrollView simpleViewScrollView = new();
	readonly SimpleView simpleView = new();

	readonly Label AffectDateLabel;
	readonly Label WorkInfoLabel;
	static Label GenAffectDateLabel()
	{
		Label v = DTACElementStyles.Instance.AffectDateLabelStyle<Label>();

		SetRow(v, 0);

		return v;
	}
	static Label GenWorkInfoLabel()
	{
		Label v = DTACElementStyles.Instance.HakoTabWorkInfoLabelStyle<Label>();

		SetRow(v, 0);

		return v;
	}

	public Hako()
	{
		logger.Trace("Creating...");

		DTACElementStyles.Instance.DefaultBGColor.Apply(this, BackgroundColorProperty);

		RowDefinitions = InstanceManager.DTACViewHostViewModel.RowDefinitionsProvider.HakoPageRowDefinitions;

		Grid.SetRow(backgroundImage, 2);
		Children.Add(backgroundImage);

		simpleViewScrollView.Content = simpleView;
		Grid.SetRow(simpleViewScrollView, 2);
		Children.Add(simpleViewScrollView);

		Grid.SetRow(headerView, 1);
		headerView.EdgeWidth = SimpleView.STA_NAME_TIME_COLUMN_WIDTH;
		headerView.LeftEdgeText = Utils.InsertBetweenChars("乗務開始".AsSpan(), '\n');
		headerView.RightEdgeText = Utils.InsertBetweenChars("乗務終了".AsSpan(), '\n');
		Children.Add(headerView);

		AffectDateLabel = GenAffectDateLabel();
		Children.Add(AffectDateLabel);

		WorkInfoLabel = GenWorkInfoLabel();
		Children.Add(WorkInfoLabel);

		simpleView.SetBinding(
			WidthRequestProperty,
			BindingBase.Create(static (ScrollView x) => x.Width, BindingMode.OneWay, source: headerView)
		);

		logger.Trace("Created");
	}

	partial void OnAffectDateChanged(string? newValue)
	{
		logger.Info("AffectDate: {0}", newValue);
		AffectDateLabel.Text = DTACElementStyles.AffectDateLabelTextPrefix + newValue;
	}

	partial void OnWorkNameChanged(string? newValue)
	{
		logger.Info("WorkName: {0}", newValue);
		UpdateWorkInfoLabel(newValue, WorkSpaceName);
	}
	partial void OnWorkSpaceNameChanged(string? newValue)
	{
		logger.Info("WorkSpaceName: {0}", newValue);
		UpdateWorkInfoLabel(WorkName, newValue);
	}

	void UpdateWorkInfoLabel(string? workName, string? workSpaceName)
	{
		WorkInfoLabel.Text = $"{workName}\n{workSpaceName}";
	}
}
