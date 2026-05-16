namespace TRViS.MyAppCustomizables;

/// <summary>
/// PDF を表示する際に使用する pdf.js のバージョンと描画方式の組み合わせ。
/// </summary>
/// <remarks>
/// <para>
/// 既定値は <see cref="V2Svg"/>。これは全 iOS (12 含む) で動作実績があり、
/// ベクター描画でズーム時もピクセル化しないため安全な初期値となる。
/// </para>
/// <para>
/// pdf.js v4 以降は SVGGraphics が削除されているため、v5 は canvas 描画のみ。
/// また v5 (pdf.js 公式 legacy ビルド) の対応下限は Safari 16.4 (= iOS 16.4) のため、
/// 設定 UI では iOS 16.4 未満で v5 を出さない。同様に v3 は Safari 13+ が必要なため
/// iOS 13 未満では v2 系のみ。保存値がその端末で動作不可の場合は
/// <see cref="PdfJsViewerHtmlBuilder"/> 側で安全な値へフォールバックする。
/// </para>
/// </remarks>
public enum PdfJsRenderEngine
{
	/// <summary>pdf.js v2 (legacy build) を SVG 描画で使用する。全 iOS で動作。</summary>
	V2Svg = 0,

	/// <summary>pdf.js v2 (legacy build) を canvas 描画で使用する。全 iOS で動作。</summary>
	V2Canvas = 1,

	/// <summary>pdf.js v3 (modern build) を SVG 描画で使用する。iOS 13 以降。</summary>
	V3Svg = 2,

	/// <summary>pdf.js v3 (modern build) を canvas 描画で使用する。iOS 13 以降。</summary>
	V3Canvas = 3,

	/// <summary>pdf.js v5 を canvas 描画で使用する。iOS 16.4 以降のみ。</summary>
	V5Canvas = 4,
}
