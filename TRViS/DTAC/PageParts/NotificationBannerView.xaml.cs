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

	// アイコンバッジの文字サイズ。1 文字なら 24pt (アイコン 56x56 に対して見栄えの良い比率)、
	// 2 文字を表示する場合は 56 幅に収まるようやや縮小する。
	private const double IconBadgeFontSizeSingleChar = 24;
	private const double IconBadgeFontSizeTwoChars = 16;

	// 受領ボタンの点滅 (緑⇔白背景/黒文字、500ms間隔) — 受領を促す。大型ポップアップ表示中は
	// SetAcknowledgeBlinkPaused(true) で一時停止し、緑固定にする (ViewHost が
	// NotificationCenterViewModel.PopupVisibilityChanged を購読して呼ぶ)。
	private static readonly TimeSpan AcknowledgeBlinkInterval = TimeSpan.FromMilliseconds(500);
	private IDispatcherTimer? _acknowledgeBlinkTimer;
	private Brush? _acknowledgeGreenBackground;
	private bool _acknowledgeBlinkGreenPhase = true;
	private bool _acknowledgeBlinkPaused;

	/// <summary>現在表示している通告の Id (Id 無し通告なら null)。</summary>
	public string? CurrentId => _entry?.Id;

	/// <summary>バナーがタップされ、大きいポップアップへ展開すべきとき発火する。</summary>
	public event EventHandler<NotificationStore.Entry>? Tapped;

	/// <summary>受領ボタンが押されたとき発火する。</summary>
	public event EventHandler<NotificationStore.Entry>? AcknowledgeClicked;

	/// <summary>バナーが上方向にスワイプされたとき発火する (画面上部への固定を促すジェスチャー)。</summary>
	public event EventHandler? SwipedUp;

	/// <summary>バナーが下方向にスワイプされたとき発火する (画面下部への固定を促すジェスチャー)。</summary>
	public event EventHandler? SwipedDown;

	public NotificationBannerView()
	{
		InitializeComponent();
		_acknowledgeGreenBackground = AcknowledgeButton.Background;
		Unloaded += (_, _) => StopAcknowledgeBlink(forceGreen: false);
	}

	/// <summary>
	/// 大型ポップアップ (拡大表示) が表示中かどうかを反映する。true の間は点滅を止めて緑固定に
	/// する — ポップアップ側で既に受領を促しているため、背後のバナーが点滅し続ける必要はない。
	/// false に戻ったとき (最小化/閉じた) は、受領ボタンが表示中であれば点滅を再開する。
	/// </summary>
	public void SetAcknowledgeBlinkPaused(bool paused)
	{
		_acknowledgeBlinkPaused = paused;
		UpdateAcknowledgeBlinkState();
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
			IconBadgeLabel.FontSize = entry.IconText.Length >= 2
				? IconBadgeFontSizeTwoChars
				: IconBadgeFontSizeSingleChar;
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

		// 要約 (Summary)。未指定/空文字なら Title、Title も無ければページタイトル = "通告"
		// にフォールバックする。本文は展開先のポップアップでのみ表示するため、ここでは
		// プレーンテキストに留める。改行を含む場合のみ 2 行まで表示を許可し (それでも収まらない
		// 分は TailTruncation で "…" 省略)、改行を含まない場合は従来通り 1 行に制限する。
		string summaryText = !string.IsNullOrEmpty(entry.Summary)
			? entry.Summary
			: string.IsNullOrEmpty(entry.Title)
				? TRViS.Localization.AppResources.Notification_Title
				: entry.Title;
		SummaryLabel.Text = summaryText;
		SummaryLabel.MaxLines = summaryText.Contains('\n') ? 2 : 1;

		// 受領ボタンは「未受領かつ受領可能」のときのみ表示する。
		AcknowledgeButton.IsVisible = !entry.IsRead && entry.CanAcknowledge;
		UpdateAcknowledgeBlinkState();
	}

	private void UpdateAcknowledgeBlinkState()
	{
		if (AcknowledgeButton.IsVisible && !_acknowledgeBlinkPaused)
			StartAcknowledgeBlink();
		else
			StopAcknowledgeBlink(forceGreen: true);
	}

	private void StartAcknowledgeBlink()
	{
		if (_acknowledgeBlinkTimer is not null)
			return;

		_acknowledgeBlinkGreenPhase = true;
		ApplyAcknowledgeBlinkPhase(green: true);

#if UI_TEST
		// UI_TEST ビルドでは点滅させない (緑固定)。点滅させたままだと、スクリーンショット
		// 回帰テストの capture が点滅のどちらの位相に当たるかで毎回異なる差分を生み、
		// 決定的に比較できなくなるため。
		return;
#else
		_acknowledgeBlinkTimer = Dispatcher.CreateTimer();
		_acknowledgeBlinkTimer.Interval = AcknowledgeBlinkInterval;
		_acknowledgeBlinkTimer.Tick += OnAcknowledgeBlinkTick;
		_acknowledgeBlinkTimer.Start();
#endif
	}

	private void StopAcknowledgeBlink(bool forceGreen)
	{
		if (_acknowledgeBlinkTimer is not null)
		{
			_acknowledgeBlinkTimer.Stop();
			_acknowledgeBlinkTimer.Tick -= OnAcknowledgeBlinkTick;
			_acknowledgeBlinkTimer = null;
		}
		if (forceGreen)
			ApplyAcknowledgeBlinkPhase(green: true);
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

	private void OnBannerSwipedUp(object? sender, SwipedEventArgs e)
		=> SwipedUp?.Invoke(this, EventArgs.Empty);

	private void OnBannerSwipedDown(object? sender, SwipedEventArgs e)
		=> SwipedDown?.Invoke(this, EventArgs.Empty);
}
