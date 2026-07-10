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

	// Id を持ち、かつ未受領の通告は「受領」でしか閉じられないようにする。
	// このとき「閉じる」ボタン・端末の戻る操作による dismiss を無効化する
	// (「最小化」は受領扱いではないため、この制限の対象外 — OnMinimizeClicked 参照)。
	// 既に受領済みの通告 (バナー等からの再表示) は通常の閉じられるポップアップとして扱う。
	private readonly bool _requireAcknowledge;

	// 受領/閉じるの二重発火 (連打・受領直後の閉じる等) で PopModalAsync が
	// 二重に走るのを防ぐ。
	private bool _isClosing;

	// このページの基準フォントサイズ (18pt)。他の寸法値はこれの倍数として設計しているが、
	// MAUI の XAML には em 単位が無いため、カードの最大高さ (25em) だけはここから
	// コードビハインドで計算する。
	private const double BaseFontSize = 18;
	private const double CardMaximumHeightEm = 25;

	// アイコンバッジの文字サイズ。1 文字なら 48pt (アイコン 72x72 に対して見栄えの良い比率)、
	// 2 文字を表示する場合は 72 幅に収まるようやや縮小する。OS のフォントサイズ設定で
	// 拡大されるとアイコンの正方形からあふれるため、XAML 側で FontAutoScalingEnabled="False"
	// にしている (このサイズ自体は常に固定値として使う)。
	private const double IconBadgeFontSizeSingleChar = 48;
	private const double IconBadgeFontSizeTwoChars = 32;

	// 受領ボタンの点滅 (緑⇔白背景/黒文字、500ms間隔) — 受領を促す。_requireAcknowledge の
	// ときのみ動く。緑側の背景ブラシは XAML (Button.Background の LinearGradientBrush) から
	// 一度だけ取得して保持し、白側との切り替えで再利用する (AppThemeBinding を保持した
	// ブラシインスタンスなのでテーマ変更にも追従する)。
	private static readonly TimeSpan AcknowledgeBlinkInterval = TimeSpan.FromMilliseconds(500);
	private IDispatcherTimer? _acknowledgeBlinkTimer;
	private Brush? _acknowledgeGreenBackground;
	private bool _acknowledgeBlinkGreenPhase = true;

	public NotificationPopupPage(NotificationStore.Entry entry, NotificationCenterViewModel viewModel)
	{
		_entry = entry;
		_viewModel = viewModel;
		// 受領済み (再表示された既読の通告) は受領を要求しない。バナー等からの
		// 再表示は通常の閉じられるポップアップとして開く。
		_requireAcknowledge = entry.CanAcknowledge && !entry.IsRead;

		InitializeComponent();

		CardBorder.MaximumHeightRequest = BaseFontSize * CardMaximumHeightEm;
		_acknowledgeGreenBackground = AcknowledgeButton.Background;

		IOSPage.SetModalPresentationStyle(
			this.On<iOS>(),
			UIModalPresentationStyle.OverFullScreen);

		ApplyEntry(entry);

		// 小型バナーは、このポップアップ (拡大表示) が出ている間は受領ボタンの点滅を止める
		// (最小化/閉じたら再開)。ViewHost がこのイベントを購読して NotificationBannerView を操作する。
		_viewModel.NotifyPopupVisibilityChanged(true);

		// WebSocket 切断等で保持中の通告が一括破棄されたら、受領必須であってもこのポップアップを
		// 自分で閉じる (この通告はもうサーバーとの整合性が取れないため)。
		_viewModel.Cleared += OnNotificationsCleared;
	}

	private void OnNotificationsCleared(object? sender, EventArgs e)
	{
		if (_isClosing)
			return;
		_isClosing = true;
		_ = CloseAsync();
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
			IconBadgeLabel.FontSize = entry.IconText.Length >= 2
				? IconBadgeFontSizeTwoChars
				: IconBadgeFontSizeSingleChar;
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

		// 指令番号 / 指令者 / 受信者 (未設定の項目は行ごと非表示)。ラベル/コロンは XAML 側の
		// 固定幅列が担うので、ここでは値だけを設定する。
		OrderNumberLabel.Text = entry.OrderNumber ?? string.Empty;
		OrderNumberRow.IsVisible = !string.IsNullOrEmpty(entry.OrderNumber);
		SenderLabel.Text = entry.Sender ?? string.Empty;
		SenderRow.IsVisible = !string.IsNullOrEmpty(entry.Sender);
		ReceiverLabel.Text = entry.Receiver ?? string.Empty;
		ReceiverRow.IsVisible = !string.IsNullOrEmpty(entry.Receiver);

		// タイトル (未設定ならページタイトル = "通告" にフォールバック)。
		TitleLabel.Text = string.IsNullOrEmpty(entry.Title)
			? TRViS.Localization.AppResources.Notification_Title
			: entry.Title;

		// 本文は BBCode。HTML 誤検出を避けるため HtmlAutoDetectLabel ではなく
		// BBCodeLabel を直接使う (通告本文は BBCode 仕様のため)。
		var bodyLabel = new BBCodeLabel
		{
			FontSize = 18,
			LineBreakMode = LineBreakMode.WordWrap,
			DefaultLightThemeTextColor = RootStyles.TableTextColor.Light,
			DefaultDarkThemeTextColor = RootStyles.TableTextColor.Dark,
			BBCodeText = entry.Body ?? string.Empty,
			AutomationId = "Notification.Body",
#if IOS || MACCATALYST
			// 等幅フォント (レイアウト崩れ防止)。Osaka-Mono は和文・半角英数字とも固定幅で
			// 揃う iOS/macOS 組み込みフォント。Android/Windows には和文対応の同等フォントが
			// 標準搭載されていないため、Apple 系のみに限定する。
			FontFamily = "Osaka-Mono",
#endif
		};
		BodyContainer.Content = bodyLabel;

		// Priority による強調表示。
		ImportantBadge.IsVisible = entry.IsImportant;

		// 発行時刻。ISO8601 (日付部分あり) としてパースできた場合のみ現在言語での自然な表記に
		// 整形する (TZ 指定ありは端末の現在 TZ に変換、TZ 指定無しはその時刻をそのまま表示)。
		// パースできなかった入力 (ISO8601 以外の任意の文字列) は、そのまま表示する。
		if (entry.IssuedAt is System.DateTimeOffset issuedAt)
		{
			var displayDateTime = entry.IssuedAtIsUnspecifiedTimeZone ? issuedAt.DateTime : issuedAt.LocalDateTime;
			IssuedAtLabel.Text = FormatIssuedAtNatural(displayDateTime);
			IssuedAtRow.IsVisible = true;
		}
		else if (!string.IsNullOrEmpty(entry.IssuedAtRawText))
		{
			IssuedAtLabel.Text = entry.IssuedAtRawText;
			IssuedAtRow.IsVisible = true;
		}
		else
		{
			IssuedAtRow.IsVisible = false;
		}

		// Id を持ち未受領の通告は「受領」を必須とし、「受領」ボタンでしか閉じられないようにする
		// (鉄道の通告受領の意図に沿う)。「閉じる」ボタンは隠すが、右上の「最小化」は受領扱いに
		// ならないため常に表示する (OnMinimizeClicked 参照)。
		// Id 無しの通告 (受領不可なお知らせ)、および既に受領済みの通告 (バナー等からの
		// 再表示) は「受領」を隠し、「閉じる」(実質は最小化と同じ動作) で閉じられる。
		AcknowledgeButton.IsVisible = _requireAcknowledge;
		DismissButton.IsVisible = !_requireAcknowledge;

		if (_requireAcknowledge)
			StartAcknowledgeBlink();
	}

	private void StartAcknowledgeBlink()
	{
		if (_acknowledgeBlinkTimer is not null)
			return;

		_acknowledgeBlinkGreenPhase = true;
		ApplyAcknowledgeBlinkPhase(green: true);

		_acknowledgeBlinkTimer = Dispatcher.CreateTimer();
		_acknowledgeBlinkTimer.Interval = AcknowledgeBlinkInterval;
		_acknowledgeBlinkTimer.Tick += OnAcknowledgeBlinkTick;
		_acknowledgeBlinkTimer.Start();
	}

	private void StopAcknowledgeBlink()
	{
		if (_acknowledgeBlinkTimer is null)
			return;

		_acknowledgeBlinkTimer.Stop();
		_acknowledgeBlinkTimer.Tick -= OnAcknowledgeBlinkTick;
		_acknowledgeBlinkTimer = null;
	}

	private void OnAcknowledgeBlinkTick(object? sender, EventArgs e)
	{
		_acknowledgeBlinkGreenPhase = !_acknowledgeBlinkGreenPhase;
		ApplyAcknowledgeBlinkPhase(_acknowledgeBlinkGreenPhase);
	}

	private void ApplyAcknowledgeBlinkPhase(bool green)
	{
		if (green)
		{
			AcknowledgeButton.BackgroundColor = Colors.Transparent;
			AcknowledgeButton.Background = _acknowledgeGreenBackground;
			AcknowledgeButton.TextColor = Colors.White;
		}
		else
		{
			AcknowledgeButton.Background = null;
			AcknowledgeButton.BackgroundColor = Colors.White;
			AcknowledgeButton.TextColor = Colors.Black;
		}
	}

	/// <summary>
	/// 発行時刻を現在の表示言語での自然な表記に整形する (例: 日本語なら
	/// "MM月dd日 HH時mm分"。年は表示しない)。日本語以外は現在カルチャの標準的な日時書式
	/// ("f": 曜日を含む長い日付 + 短い時刻) にフォールバックする。
	/// </summary>
	private static string FormatIssuedAtNatural(DateTime dateTime)
	{
		var culture = TRViS.Localization.LocalizationResourceManager.Current.CurrentCulture;
		return culture.TwoLetterISOLanguageName switch
		{
			"ja" => dateTime.ToString("MM月dd日 HH時mm分", System.Globalization.CultureInfo.InvariantCulture),
			_ => dateTime.ToString("f", culture),
		};
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

	// 「最小化」: 通告を消さず、D-TAC 画面上部の小型バナー表示に切り替える。受領必須の
	// 通告 (_requireAcknowledge) でも許可する — 最小化は受領扱いではなく、単に表示形態を
	// 変えるだけなので、受領必須ガードは適用しない。「閉じる」ボタン (DismissButton) も
	// これと同じ挙動に統一している (単独の「完全に破棄する」動作は存在しない)。
	private async void OnMinimizeClicked(object? sender, EventArgs e)
	{
		if (_isClosing)
			return;
		_isClosing = true;
		_viewModel.RequestBannerDisplay(_entry);
		await CloseAsync();
	}

	private async Task CloseAsync()
	{
		StopAcknowledgeBlink();
		// このポップアップが消えるので、小型バナーの受領ボタンの点滅を再開させる。
		_viewModel.NotifyPopupVisibilityChanged(false);
		_viewModel.Cleared -= OnNotificationsCleared;

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
