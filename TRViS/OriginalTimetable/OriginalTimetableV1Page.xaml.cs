using TRViS.Services;

namespace TRViS.OriginalTimetable;

// V1 Modern Classic — 独自時刻表ページ骨格
// PROBE: minimal CollectionView prototype to verify the iPad mini A17
// ApplyStyleSheets NRE does not reproduce with MAUI-stock virtualized list.
public partial class OriginalTimetableV1Page : ContentPage
{
	public static readonly string NameOfThisClass = nameof(OriginalTimetableV1Page);

	public List<RowVM> Rows { get; } = new()
	{
		new RowVM("田端",   "08:30", "08:32", "1番", "A", false),
		new RowVM("",       "",      "",      "",    "▼ 区間切替 — 最高 110km/h", true),
		new RowVM("赤羽",   "08:40", "08:42", "2番", "A", false),
		new RowVM("浦和",   "08:55", "08:57", "3番", "B", false),
		new RowVM("大宮",   "09:10", "09:13", "5番", "B", false),
		new RowVM("",       "",      "",      "",    "▼ 区間切替 — 最高 95km/h",  true),
		new RowVM("蓮田",   "09:25", "09:26", "1番", "C", false),
		new RowVM("白岡",   "09:32", "09:33", "1番", "C", false),
		new RowVM("久喜",   "09:40", "09:42", "2番", "C", false),
		new RowVM("古河",   "09:55", "09:57", "1番", "C", false),
		new RowVM("小山",   "10:10", "10:13", "3番", "D", false),
		new RowVM("宇都宮", "10:35", "—",     "5番", "D", false),
	};

	public OriginalTimetableV1Page()
	{
		InitializeComponent();
		BindingContext = InstanceManager.OriginalTimetableViewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.Portrait);
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
	}
}

// PROBE row VM. Independent of TrainData/TimetableRow on purpose — verifying
// MAUI render path only, not real-data wiring.
public record RowVM(
	string StationName,
	string Arrive,
	string Depart,
	string Track,
	string Run,
	bool IsSectionBreak)
{
	public bool IsNormalRow => !IsSectionBreak;
}
