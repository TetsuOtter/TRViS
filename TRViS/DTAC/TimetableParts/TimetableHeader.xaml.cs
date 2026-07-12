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
		// 同じ static 述語 (実ビュー幅のみで判定、端末フル幅へのフォールバックなし。
		// issue #320) を経由するので食い違わない。
		VerticalTimetableColumnVisibilityState.ViewWidthMode? lastMode = null;
		SizeChanged += (_, _) =>
		{
			if (Width <= 0)
				return;
			VerticalTimetableColumnVisibilityState.ViewWidthMode mode
				= VerticalTimetableColumnVisibilityState.ClassifyWidth(Width);
			if (lastMode == mode)
				return;
			lastMode = mode;

			RunTimeLabel.IsVisible = RunTimeSeparator.IsVisible
				= VerticalTimetableColumnVisibilityState.IsRunTimeVisible(mode);
			LimitLabel.IsVisible = LimitSeparator.IsVisible
				= VerticalTimetableColumnVisibilityState.IsRunInOutLimitVisible(mode);
			RemarksLabel.IsVisible = VerticalTimetableColumnVisibilityState.IsRemarksVisible(mode);
			MarkerBtn.IsVisible = VerticalTimetableColumnVisibilityState.IsMarkerVisible(mode);
		};

		logger.Trace("Created");
	}
}
