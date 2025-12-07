using DependencyPropertyGenerator;

using TRViS.DTAC.HakoParts;
using TRViS.Services;
using TRViS.Utils;

namespace TRViS.DTAC;

[DependencyProperty<string>("AffectDate")]
[DependencyProperty<string>("WorkName")]
[DependencyProperty<string>("WorkSpaceName")]
public partial class Hako : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	readonly HeaderView headerView = [];

	readonly Label AffectDateLabel;
	readonly Label WorkInfoLabel;
	static Label GenAffectDateLabel()
	{
		Label v = DTACElementStyles.AffectDateLabelStyle<Label>();

		SetRow(v, 0);

		return v;
	}
	static Label GenWorkInfoLabel()
	{
		Label v = DTACElementStyles.HakoTabWorkInfoLabelStyle<Label>();

		SetRow(v, 0);

		return v;
	}

	public Hako()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		Grid.SetRow(headerView, 1);
		headerView.EdgeWidth = SimpleView.STA_NAME_TIME_COLUMN_WIDTH;
		headerView.LeftEdgeText = Util.InsertBetweenChars("乗務開始".AsSpan(), '\n');
		headerView.RightEdgeText = Util.InsertBetweenChars("乗務終了".AsSpan(), '\n');
		Children.Add(headerView);

		AffectDateLabel = GenAffectDateLabel();
		Children.Add(AffectDateLabel);

		WorkInfoLabel = GenWorkInfoLabel();
		Children.Add(WorkInfoLabel);

		SimpleView.SetBinding(
			WidthRequestProperty,
			BindingBase.Create(static (ScrollView x) => x.Width, BindingMode.OneWay, source: headerView)
		);

		SimpleView.IsBusyChanged += (s, _) =>
		{
			if (s is not SimpleView v)
				return;

			logger.Info("IsBusyChanged: {0}", v.IsBusy);

			MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
					if (v.IsBusy)
					{
						SimpleViewActivityIndicatorBorder.IsVisible = true;
						SimpleViewActivityIndicatorBorder.FadeTo(VerticalStylePage.TimetableViewActivityIndicatorBorderMaxOpacity);
					}
					else
					{
						SimpleViewActivityIndicatorBorder.FadeTo(0).ContinueWith((_) =>
						{
							logger.Debug("SimpleViewActivityIndicatorBorder.FadeTo(0) completed");
							SimpleViewActivityIndicatorBorder.IsVisible = false;
						});
					}
				}
				catch (Exception ex)
				{
					logger.Fatal(ex, "Unknown Exception");
					InstanceManager.CrashlyticsWrapper.Log(ex, "Hako.SimpleView.IsBusyChanged");
					Util.ExitWithAlert(ex);
				}
			});
		};

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
