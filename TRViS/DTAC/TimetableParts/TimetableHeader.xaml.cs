using DependencyPropertyGenerator;

using TRViS.DTAC.ViewModels;
using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<double>("FontSize_Large")]
public partial class TimetableHeader : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public TimetableHeader()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		DTACElementStyles.SetTimetableColumnWidthCollection(this);

		// issue #41: 列幅が 0 へ畳まれた列のヘッダ見出しも非表示にする。
		// 幅判定は SetTimetableColumnWidthCollection / ColumnVisibilityState と
		// 同じ static 述語を経由するので食い違わない。
		VerticalTimetableColumnVisibilityState.ViewWidthMode? lastMode = null;
		SizeChanged += (_, _) =>
		{
			if (Width <= 0)
				return;
			VerticalTimetableColumnVisibilityState.ViewWidthMode m
				= VerticalTimetableColumnVisibilityState.ClassifyWidth(Width);
			if (lastMode == m)
				return;
			lastMode = m;

			RunTimeLabel.IsVisible = RunTimeSeparator.IsVisible
				= VerticalTimetableColumnVisibilityState.IsRunTimeVisible(m);
			LimitLabel.IsVisible = LimitSeparator.IsVisible
				= VerticalTimetableColumnVisibilityState.IsRunInOutLimitVisible(m);
			RemarksLabel.IsVisible = VerticalTimetableColumnVisibilityState.IsRemarksVisible(m);
			MarkerBtn.IsVisible = VerticalTimetableColumnVisibilityState.IsMarkerVisible(m);
		};

		logger.Trace("Created");
	}
}
