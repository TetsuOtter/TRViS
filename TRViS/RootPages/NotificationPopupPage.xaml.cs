using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

using TR.BBCodeLabel.Maui;

using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

using IOSPage = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page;

namespace TRViS.RootPages;

/// <summary>
/// サーバーから受信した通告 (Notification) を表示するモーダルポップアップ。
/// タイトル・本文 (BBCode) を表示し、Priority が 1 以上のとき「重要」バッジで強調する。
/// 「受領」ボタンで <see cref="NotificationCenterViewModel.AcknowledgeAsync"/> を呼び、
/// サーバーへ受領を通知してから閉じる。
/// </summary>
public partial class NotificationPopupPage : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private readonly NotificationCenterViewModel _viewModel;
	private readonly NotificationStore.Entry _entry;

	// Id を持つ通告 (受領可能) は「受領」でしか閉じられないようにする。
	// このとき「閉じる」ボタン・ヘッダーの×・端末の戻る操作による dismiss を無効化する。
	private readonly bool _requireAcknowledge;

	// 受領/閉じるの二重発火 (連打・受領直後の閉じる等) で PopModalAsync が
	// 二重に走るのを防ぐ。
	private bool _isClosing;

	public NotificationPopupPage(NotificationStore.Entry entry, NotificationCenterViewModel viewModel)
	{
		_entry = entry;
		_viewModel = viewModel;
		_requireAcknowledge = entry.CanAcknowledge;

		InitializeComponent();

		// 受領必須 (Id あり) の通告は FullScreen で表示する。FullScreen モーダルは
		// スワイプで dismiss できないため、iOS でも「受領」以外で閉じられない。
		// 受領不可 (Id 無し) のお知らせは数行が全画面を占有しないよう FormSheet で
		// 中央表示する (スワイプで閉じられてもよい)。
		// iPhone は compact 幅で UIKit が FormSheet を自動的に全画面へフォールバックする。
		IOSPage.SetModalPresentationStyle(
			this.On<iOS>(),
			_requireAcknowledge ? UIModalPresentationStyle.FullScreen : UIModalPresentationStyle.FormSheet);

		ApplyEntry(entry);
	}

	private void ApplyEntry(NotificationStore.Entry entry)
	{
		// アイコン: 画像 (Base64) が優先。無ければ背景色+文字のバッジ。どちらも無ければ非表示。
		if (!string.IsNullOrEmpty(entry.IconImageBase64) && TryDecodeIconImage(entry.IconImageBase64, out var iconSource))
		{
			IconImage.Source = iconSource;
			IconImage.IsVisible = true;
			IconBadge.IsVisible = false;
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
		}
		else
		{
			IconBadge.IsVisible = false;
			IconImage.IsVisible = false;
		}

		// 指令番号 / 指令者 / 受信者 (未設定の項目は行ごと非表示)。
		OrderNumberLabel.Text = string.Format(TRViS.Localization.AppResources.Notification_OrderNumberFormat, entry.OrderNumber);
		OrderNumberLabel.IsVisible = !string.IsNullOrEmpty(entry.OrderNumber);
		SenderLabel.Text = string.Format(TRViS.Localization.AppResources.Notification_SenderFormat, entry.Sender);
		SenderLabel.IsVisible = !string.IsNullOrEmpty(entry.Sender);
		ReceiverLabel.Text = string.Format(TRViS.Localization.AppResources.Notification_ReceiverFormat, entry.Receiver);
		ReceiverLabel.IsVisible = !string.IsNullOrEmpty(entry.Receiver);

		// タイトル (未設定ならページタイトル = "通告" にフォールバック)。
		TitleLabel.Text = string.IsNullOrEmpty(entry.Title)
			? TRViS.Localization.AppResources.Notification_Title
			: entry.Title;

		// 本文は BBCode。HTML 誤検出を避けるため HtmlAutoDetectLabel ではなく
		// BBCodeLabel を直接使う (通告本文は BBCode 仕様のため)。
		var bodyLabel = new BBCodeLabel
		{
			FontSize = 16,
			LineBreakMode = LineBreakMode.WordWrap,
			DefaultLightThemeTextColor = RootStyles.TableTextColor.Light,
			DefaultDarkThemeTextColor = RootStyles.TableTextColor.Dark,
			BBCodeText = entry.Body ?? string.Empty,
			AutomationId = "Notification.Body",
		};
		BodyContainer.Content = bodyLabel;

		// Priority による強調表示。
		ImportantBadge.IsVisible = entry.IsImportant;

		// 発行時刻。TZ 指定ありは端末の現在 TZ に変換して表示、TZ 指定無しはその時刻をそのまま表示する。
		if (entry.IssuedAt is System.DateTimeOffset issuedAt)
		{
			var displayDateTime = entry.IssuedAtIsUnspecifiedTimeZone ? issuedAt.DateTime : issuedAt.LocalDateTime;
			IssuedAtLabel.Text = displayDateTime.ToString("yyyy/MM/dd HH:mm");
			IssuedAtLabel.IsVisible = true;
		}

		// Id を持つ通告 (受領可能) は「受領」を必須とし、「受領」ボタンでしか閉じられないようにする。
		// ヘッダーの× と「閉じる」ボタンは隠す (鉄道の通告受領の意図に沿う)。
		// Id 無しの通告 (受領不可なお知らせ) はサーバーへ受領できないため
		// 「受領」を隠し、「閉じる」/× のみで閉じられるようにする。
		AcknowledgeButton.IsVisible = entry.CanAcknowledge;
		DismissButton.IsVisible = !entry.CanAcknowledge;
		CloseButton.IsVisible = !entry.CanAcknowledge;
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
			logger.Warn(ex, "Failed to decode notification icon image");
			return false;
		}
	}

	// Android のハードウェア戻るボタン等による dismiss を、受領必須の通告では無効化する。
	protected override bool OnBackButtonPressed()
	{
		if (_requireAcknowledge)
			return true; // 戻る操作を握りつぶし、閉じさせない。
		return base.OnBackButtonPressed();
	}

	private async void OnAcknowledgeClicked(object? sender, EventArgs e)
	{
		if (_isClosing)
			return;
		_isClosing = true;

		// オンライン: 送信が確定したときのみ既読化される (AcknowledgeAsync 内)。
		// オフライン/送信失敗: 既読化されず、サーバーへは何も送信されない。いずれの場合も
		// ポップアップは閉じる (乗務員をブロックしない)。未受領の通告の再表示はサーバーの
		// 再配信に委ねる。戻り値 (送信成否) はここでは分岐に使わない。
		try
		{
			await _viewModel.AcknowledgeAsync(_entry);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "AcknowledgeAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "NotificationPopupPage.OnAcknowledgeClicked");
		}
		await CloseAsync();
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		if (_isClosing)
			return;
		_isClosing = true;
		await CloseAsync();
	}

	private async Task CloseAsync()
	{
		try
		{
			await Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "PopModalAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "NotificationPopupPage.CloseAsync");
		}
	}
}
