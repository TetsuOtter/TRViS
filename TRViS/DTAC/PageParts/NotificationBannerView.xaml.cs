using TRViS.Services;
using TRViS.Utils;

namespace TRViS.DTAC;

/// <summary>
/// D-TAC 画面上部に重ねて出す小型の通告バナー。表示内容は完全に <see cref="Configure"/> の
/// 引数 (<see cref="NotificationStore.Entry"/>) に従うだけの受け身のビューで、
/// NotificationCenterViewModel のイベント購読・表示/非表示の管理は ViewHost が担う。
/// タップで展開 (<see cref="Tapped"/>)・受領ボタンで受領 (<see cref="AcknowledgeClicked"/>) を
/// それぞれ通知するのみで、自分自身を隠す判断はしない (ViewModel 側の
/// BannerRequested/BannerDismissed に委ねる)。
/// </summary>
public partial class NotificationBannerView : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private NotificationStore.Entry? _entry;

	/// <summary>現在表示している通告の Id (Id 無し通告なら null)。</summary>
	public string? CurrentId => _entry?.Id;

	/// <summary>バナーがタップされ、大きいポップアップへ展開すべきとき発火する。</summary>
	public event EventHandler<NotificationStore.Entry>? Tapped;

	/// <summary>受領ボタンが押されたとき発火する。</summary>
	public event EventHandler<NotificationStore.Entry>? AcknowledgeClicked;

	public NotificationBannerView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// 表示内容を指定の通告で更新する。アイコン・概要・受領ボタンの表示可否
	/// (!entry.IsRead &amp;&amp; entry.CanAcknowledge のときのみ表示) を反映する。
	/// </summary>
	public void Configure(NotificationStore.Entry entry)
	{
		_entry = entry;

		// アイコン: 画像 (Base64) が優先。無ければ背景色+文字のバッジ。どちらも無ければ
		// IconHost ごと非表示にして Col0 (Auto) を畳む。
		if (!string.IsNullOrEmpty(entry.IconImageBase64) && TryDecodeIconImage(entry.IconImageBase64, out var iconSource))
		{
			IconImage.Source = iconSource;
			IconImage.IsVisible = true;
			IconBadge.IsVisible = false;
			IconHost.IsVisible = true;
		}
		else if (!string.IsNullOrEmpty(entry.IconText))
		{
			IconBadgeLabel.Text = entry.IconText;
			if (entry.IconColor_RGB is int rgb)
				IconBadge.BackgroundColor = Color.FromRgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
			else
				RootStyles.TableDetailColor.Apply(IconBadge, BackgroundColorProperty);
			IconBadge.IsVisible = true;
			IconImage.IsVisible = false;
			IconHost.IsVisible = true;
		}
		else
		{
			IconBadge.IsVisible = false;
			IconImage.IsVisible = false;
			IconHost.IsVisible = false;
		}

		// タイトル (未設定ならページタイトル = "通告" にフォールバック)。本文は展開先の
		// ポップアップでのみ表示するため、ここでは 1 行のプレーンテキストに留める。
		SummaryLabel.Text = string.IsNullOrEmpty(entry.Title)
			? TRViS.Localization.AppResources.Notification_Title
			: entry.Title;

		// 受領ボタンは「未受領かつ受領可能」のときのみ表示する。
		AcknowledgeButton.IsVisible = !entry.IsRead && entry.CanAcknowledge;
	}

	/// <summary>
	/// アイコン画像の Base64 文字列 (data URI プレフィックス <c>data:image/...;base64,</c> を
	/// 含んでいてもよい) を <see cref="ImageSource"/> にデコードする。不正な値は false を返し、
	/// バッジ表示へフォールバックさせる。
	/// </summary>
	private static bool TryDecodeIconImage(string base64, out ImageSource? source)
	{
		source = null;
		try
		{
			int commaIndex = base64.IndexOf(',');
			string payload = base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0
				? base64[(commaIndex + 1)..]
				: base64;
			byte[] bytes = Convert.FromBase64String(payload);
			source = ImageSource.FromStream(() => new MemoryStream(bytes));
			return true;
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "Failed to decode notification banner icon image");
			return false;
		}
	}

	private void OnBannerTapped(object? sender, EventArgs e)
	{
		if (_entry is NotificationStore.Entry entry)
			Tapped?.Invoke(this, entry);
	}

	private void OnAcknowledgeButtonClicked(object? sender, EventArgs e)
	{
		if (_entry is NotificationStore.Entry entry)
			AcknowledgeClicked?.Invoke(this, entry);
	}
}
