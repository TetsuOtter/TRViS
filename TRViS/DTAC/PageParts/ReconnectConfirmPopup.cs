using TR.Maui.AnchorPopover;

using TRViS.Services;

namespace TRViS.DTAC;

/// <summary>
/// AppBar の接続ステータス (赤丸 = 切断) をタップしたときに出る確認ポップ
/// オーバー (#266)。「再接続しますか?」と確認し、再接続 / キャンセルを選ばせる。
/// AppBar.cs がコードベースなので、これも XAML を使わずコードのみで構築する。
/// </summary>
public sealed class ReconnectConfirmPopup : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	// Mirrors AutomationIds.ReconnectPopup.* (test project). Inlined so
	// production code carries no dependency on the test assembly.
	private const string PromptAutomationId = "ReconnectPopup.Prompt";
	private const string ConfirmAutomationId = "ReconnectPopup.ConfirmButton";
	private const string CancelAutomationId = "ReconnectPopup.CancelButton";

	private IAnchorPopover? _popover;
	private readonly Action _onConfirm;

	public ReconnectConfirmPopup(Action onConfirm)
	{
		ArgumentNullException.ThrowIfNull(onConfirm);
		_onConfirm = onConfirm;

		var prompt = new Label
		{
			AutomationId = PromptAutomationId,
			Text = "再接続しますか?",
			FontAttributes = FontAttributes.Bold,
			FontSize = 16,
			HorizontalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.Center,
		};
		DTACElementStyles.DefaultTextColor.Apply(prompt, Label.TextColorProperty);

		var cancelButton = new Button
		{
			AutomationId = CancelAutomationId,
			Text = "キャンセル",
			FontSize = 14,
			HorizontalOptions = LayoutOptions.Fill,
		};
		cancelButton.Clicked += OnCancelClicked;

		var confirmButton = new Button
		{
			AutomationId = ConfirmAutomationId,
			Text = "再接続",
			FontSize = 14,
			FontAttributes = FontAttributes.Bold,
			HorizontalOptions = LayoutOptions.Fill,
		};
		confirmButton.Clicked += OnConfirmClicked;

		var buttonRow = new Grid
		{
			ColumnSpacing = 8,
			Margin = new Thickness(0, 12, 0, 0),
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Star),
			},
		};
		buttonRow.Add(cancelButton, 0, 0);
		buttonRow.Add(confirmButton, 1, 0);

		Content = new VerticalStackLayout
		{
			Padding = new Thickness(16),
			Spacing = 0,
			Children = { prompt, buttonRow },
		};

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);
	}

	internal void SetPopover(IAnchorPopover popover) => _popover = popover;

	private async void OnCancelClicked(object? sender, EventArgs e)
	{
		logger.Info("ReconnectConfirmPopup: cancelled");
		if (_popover is not null)
			await _popover.DismissAsync();
	}

	private async void OnConfirmClicked(object? sender, EventArgs e)
	{
		logger.Info("ReconnectConfirmPopup: reconnect confirmed");
		if (_popover is not null)
			await _popover.DismissAsync();
		// AppBar 側で再接続フローを実行する (ぐるぐる表示 + ReconnectWebSocketAsync)。
		_onConfirm();
	}
}
