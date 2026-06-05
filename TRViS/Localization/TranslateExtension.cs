using Microsoft.Maui.Controls.Xaml;

namespace TRViS.Localization;

/// <summary>
/// XAML 用ローカライズ拡張。<c>{loc:Translate Some_Key}</c> のように使う。
///
/// <see cref="LocalizationResourceManager"/> のインデクサへの OneWay
/// バインディングを返すため、言語変更時 (<c>PropertyChanged("Item[]")</c>)
/// にバインド先のラベルが自動更新される。
/// </summary>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class TranslateExtension : IMarkupExtension<BindingBase>
{
	/// <summary>resx のキー名。</summary>
	public string Key { get; set; } = string.Empty;

	/// <summary>
	/// 任意。指定すると <c>string.Format</c> 用の単一引数バインドとして
	/// 振る舞う (現状未使用だが将来の StringFormat 連携用に予約)。
	/// </summary>
	public string? StringFormat { get; set; }

	public BindingBase ProvideValue(IServiceProvider serviceProvider)
		=> new Binding($"[{Key}]", BindingMode.OneWay, source: LocalizationResourceManager.Current)
		{
			StringFormat = StringFormat,
		};

	object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
		=> ((IMarkupExtension<BindingBase>)this).ProvideValue(serviceProvider);
}
