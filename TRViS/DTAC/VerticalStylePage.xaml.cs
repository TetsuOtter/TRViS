using TRViS.Models;

namespace TRViS.DTAC;

public partial class VerticalStylePage : ContentPage
{
	public VerticalStylePage()
	{
		InitializeComponent();

		BindingContext = new TrainData(
			"臨B999行路",
			new(2022, 9, 15),
			"工9911",
			"75",
			"通貨75D5",
			"EF６５形\n現車　　　８両\n換算　２３．４",
			9,
			"(工９９１０から)\n(乗継)",
			"テスト用データ"
			);

		MyList.ItemsSource = new TimetableRow[]
		{
			new(1, 3, "大宮", false, false, true, true, new(12,34,56), new(12,34,56), "12", 12, 34, "テスト"),
			new(1, 34, "さ新都心", false, false, false, false, new(12,34,56), new(1,34,56), "中", 12, 34, "テスト"),
			new(12, 4, "南浦和", false, false, false, false, new(12,34,56), new(12,3,56), "外２", 12, 34, "テスト"),
			new(12, 34, "津", true, false, false, false, new(12,34,56), new(12,34,5), "12", 12, 34, "テスト"),
			new(12, 34, "1", false, true, false, false, new(12,34,56), new(1,3,56), "12", 12, 34, "テスト"),
			new(12, 34, "2", false, false, false, false, new(12,34,56), new(1,34,5), "12", 12, 34, "テスト"),
			new(12, 34, "3", false, false, false, false, new(12,34,56), new(12,3,5), "12", 12, 34, "テスト"),
			new(12, 34, "4", false, false, false, false, new(12,34,56), new(1,3,5), "12", 12, 34, "テスト"),
			new(12, 34, "5", false, false, false, false, new(12,34,56), new(null,34,56), "12", 12, 34, "テスト"),
			new(12, 34, "6", false, false, false, false, new(12,34,56), new(12,34,null), "12", 12, 34, "テスト"),
			new(12, 34, "7", false, false, false, false, new(12,34,56), new(null,34,null), "12", 12, 34, "テスト"),
			new(12, 34, "8", false, false, false, false, new(12,34,56), null, "12", 12, 34, "テスト"),
			new(12, 34, "9", false, false, false, false, new(12,34,56), new(12,34,56), "12", 12, 34, "テスト"),
			new(12, 34, "A", false, false, false, false, new(12,34,56), new(12,34,56), "12", 12, 34, "テスト"),
			new(12, 34, "B", false, false, false, false, new(12,34,56), new(12,34,56), "12", 12, 34, "テスト"),
			new(12, 34, "C", false, false, false, false, new(12,34,56), new(12,34,56), "12", 12, 34, "テスト"),
			new(12, 34, "D", false, false, false, false, new(12,34,56), new(12,34,56), "12", 12, 34, "テスト"),
			new(12, 34, "E", false, false, false, false, new(12,34,56), new(12,34,56), "12", 12, 34, "テスト"),
			new(12, 34, "F", false, false, false, false, new(12,34,56), new(12,34,56), "12", 12, 34, "テスト"),
		};

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
			Content = new ScrollView()
			{
				Content = this.Content
			};
	}
}
