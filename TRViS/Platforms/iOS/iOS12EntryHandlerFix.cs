using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

using UIKit;

namespace TRViS;

/// <summary>
/// iOS 12 互換: <see cref="EntryHandler"/> の <c>ClearButtonVisibility</c> / <c>TextColor</c>
/// マッピングを差し替える。MAUI 既定実装はいずれの経路でも <c>UpdateClearButtonColor</c> から
/// 内部 "clearButton" の Highlighted 画像をティント加工するが、iOS 12 では同画像が
/// null のため <c>GetClearButtonTintImage(image.Size ...)</c> で NRE になる
/// (UIKit が iOS 13+ でのみ遅延設定する)。Issue #241。
/// 差し替え後はティント処理を行わずに <see cref="UITextField.ClearButtonMode"/> および
/// テキスト色のみを設定する。iOS 12 では既定外観のまま X ボタンは表示されるので
/// 機能上の影響はない。
/// </summary>
internal static class iOS12EntryHandlerFix
{
	public static void Apply()
	{
		EntryHandler.Mapper.Add(
			nameof(IEntry.ClearButtonVisibility),
			static (handler, entry) =>
			{
				UITextField tf = handler.PlatformView;
				tf.ClearButtonMode =
					entry.ClearButtonVisibility == ClearButtonVisibility.WhileEditing
						? UITextFieldViewMode.WhileEditing
						: UITextFieldViewMode.Never;
			});

		EntryHandler.Mapper.Add(
			nameof(IEntry.TextColor),
			static (handler, entry) => handler.PlatformView?.UpdateTextColor(entry));
	}
}
