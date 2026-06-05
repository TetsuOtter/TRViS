using System.Collections.Concurrent;

using TRViS.MyAppCustomizables;

namespace TRViS.Services;

/// <summary>
/// PDF.js を内蔵した自己完結型 HTML を生成する。
/// 全プラットフォームで同一の WebView レンダリング経路を実現するため、
/// pdf.js / pdf.worker は base64 で埋め込み、Blob URL 経由で実行させる。
/// </summary>
/// <remarks>
/// <para>
/// 使用する pdf.js のバージョンと描画方式は <see cref="PdfJsRenderEngine"/> で指定する。
/// </para>
/// <list type="bullet">
///   <item>v2 (pdfjs/legacy/, 2.16.105) — classic script。SVG / canvas 両対応。全 iOS で動作。</item>
///   <item>v3 (pdfjs/, 3.11.174) — classic script。SVG / canvas 両対応。iOS 13 以降。</item>
///   <item>v5 (pdfjs/v5/, 5.7.284 legacy build) — ES module。canvas のみ (v4 以降 SVGGraphics 廃止)。
///   pdf.js 公式の対応下限は Safari 16.4 のため iOS 16.4 以降。</item>
/// </list>
/// <para>
/// 保存値がその端末で動作不可 (iOS&lt;13 で v3、iOS&lt;16.4 で v5) の場合は
/// <see cref="ResolveForPlatform"/> で安全側 (v2 SVG) へフォールバックする。
/// </para>
/// </remarks>
internal static class PdfJsViewerHtmlBuilder
{
	// classic script (UMD)。pdfjsLib をグローバルに公開する。
	private const string V2MainAssetPath = "pdfjs/legacy/pdf.min.js";
	private const string V2WorkerAssetPath = "pdfjs/legacy/pdf.worker.min.js";
	private const string V3MainAssetPath = "pdfjs/pdf.min.js";
	private const string V3WorkerAssetPath = "pdfjs/pdf.worker.min.js";

	// ES module (.mjs)。動的 import で名前空間を取得し、worker は module worker。
	private const string V5MainAssetPath = "pdfjs/v5/pdf.min.mjs";
	private const string V5WorkerAssetPath = "pdfjs/v5/pdf.worker.min.mjs";

	private const string MainPlaceholder = "__PDFJS_MAIN_B64__";
	private const string WorkerPlaceholder = "__PDFJS_WORKER_B64__";
	private const string PdfPlaceholder = "__PDF_B64__";
	private const string UseCanvasPlaceholder = "__USE_CANVAS__";
	private const string UseModulePlaceholder = "__USE_MODULE__";

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private static readonly ConcurrentDictionary<string, Task<string>> _assetBase64Cache = new();

	/// <summary>
	/// PDF バイト列 (base64 文字列) を受け取り、指定エンジンで全ページをレンダリングする
	/// 自己完結型 HTML を返す。pdf.js のスクリプト本体はパス単位でキャッシュされる。
	/// </summary>
	/// <param name="pdfBase64">PDF バイナリの base64 表現 (Formatter から受け取る Payload)。</param>
	/// <param name="engine">使用する pdf.js のバージョンと描画方式。</param>
	/// <returns>WebView.Source = HtmlWebViewSource{Html=…} に流せる HTML 文字列。</returns>
	public static async Task<string> BuildAsync(string pdfBase64, PdfJsRenderEngine engine)
	{
		PdfJsRenderEngine resolved = ResolveForPlatform(engine);
		var (mainPath, workerPath, useCanvas, useModule) = MapEngine(resolved);

		var mainTask = _assetBase64Cache.GetOrAdd(mainPath, ReadAssetAsBase64Async);
		var workerTask = _assetBase64Cache.GetOrAdd(workerPath, ReadAssetAsBase64Async);

		string mainB64 = await mainTask.ConfigureAwait(false);
		string workerB64 = await workerTask.ConfigureAwait(false);

		return BuildHtml(pdfBase64, mainB64, workerB64, useCanvas, useModule);
	}

	/// <summary>
	/// 端末が実行できないエンジンを安全な既定値 (v2 SVG) へ落とす。
	/// v3 は Safari 13+ (nullish coalescing) が必要、v5 は iOS 16.4 以降が必要
	/// (pdf.js 公式 legacy ビルドの対応下限 Safari 16.4)。
	/// </summary>
	private static PdfJsRenderEngine ResolveForPlatform(PdfJsRenderEngine engine)
	{
		if (!OperatingSystem.IsIOS())
			return engine;

		bool v3Unusable = (engine is PdfJsRenderEngine.V3Svg or PdfJsRenderEngine.V3Canvas)
			&& !OperatingSystem.IsIOSVersionAtLeast(13);
		bool v5Unusable = engine is PdfJsRenderEngine.V5Canvas
			&& !OperatingSystem.IsIOSVersionAtLeast(16, 4);

		if (v3Unusable || v5Unusable)
		{
			logger.Warn("PDF engine {0} is not runnable on this iOS version; falling back to V2Svg", engine);
			return PdfJsRenderEngine.V2Svg;
		}

		return engine;
	}

	private static (string MainPath, string WorkerPath, bool UseCanvas, bool UseModule) MapEngine(PdfJsRenderEngine engine)
		=> engine switch
		{
			PdfJsRenderEngine.V2Svg => (V2MainAssetPath, V2WorkerAssetPath, false, false),
			PdfJsRenderEngine.V2Canvas => (V2MainAssetPath, V2WorkerAssetPath, true, false),
			PdfJsRenderEngine.V3Svg => (V3MainAssetPath, V3WorkerAssetPath, false, false),
			PdfJsRenderEngine.V3Canvas => (V3MainAssetPath, V3WorkerAssetPath, true, false),
			PdfJsRenderEngine.V5Canvas => (V5MainAssetPath, V5WorkerAssetPath, true, true),
			_ => (V2MainAssetPath, V2WorkerAssetPath, false, false),
		};

	private static async Task<string> ReadAssetAsBase64Async(string path)
	{
		try
		{
			using var stream = await FileSystem.OpenAppPackageFileAsync(path).ConfigureAwait(false);
			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms).ConfigureAwait(false);
			return Convert.ToBase64String(ms.ToArray());
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to load PDF.js asset: {0}", path);
			throw;
		}
	}

	private static string BuildHtml(string pdfBase64, string mainBase64, string workerBase64, bool useCanvas, bool useModule)
	{
		string js = ViewerJsTemplate
			.Replace(MainPlaceholder, mainBase64)
			.Replace(WorkerPlaceholder, workerBase64)
			.Replace(PdfPlaceholder, pdfBase64)
			.Replace(UseCanvasPlaceholder, useCanvas ? "true" : "false")
			.Replace(UseModulePlaceholder, useModule ? "true" : "false");

		return HtmlShellTemplate.Replace("__VIEWER_JS__", js);
	}

	private const string HtmlShellTemplate = """
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=yes">
<style>
html,body{margin:0;padding:0;background:#525659;}
#pages{display:flex;flex-direction:column;align-items:center;padding:8px 0;gap:8px;}
.page{background:#fff;box-shadow:0 2px 8px rgba(0,0,0,0.3);max-width:100%;height:auto;display:block;}
#status{color:#ddd;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;padding:16px;text-align:center;}
</style>
</head>
<body>
<div id="status">Loading PDF…</div>
<div id="pages"></div>
<script>
__VIEWER_JS__
</script>
</body>
</html>
""";

	// JS は ES5 互換 (var / function 宣言) で書く。iOS 12 WebView (v2) 含め広い互換性を保つ。
	// classic build (v2/v3) は <script src=blob> で pdfjsLib をグローバルに読み込む。
	// ES module build (v5) は <script type=module> + 動的 import で名前空間を取得し、
	// worker は明示的に {type:'module'} で生成して GlobalWorkerOptions.workerPort へ渡す。
	// SVG/canvas の切り替えは USE_CANVAS で行う (v5 は canvas のみ)。
	private const string ViewerJsTemplate = """
(function(){
var MAIN_B64='__PDFJS_MAIN_B64__';
var WORKER_B64='__PDFJS_WORKER_B64__';
var PDF_B64='__PDF_B64__';
var USE_CANVAS=__USE_CANVAS__;
var USE_MODULE=__USE_MODULE__;

function b64ToBytes(b){var s=atob(b);var u=new Uint8Array(s.length);for(var i=0;i<s.length;i++)u[i]=s.charCodeAt(i);return u;}
function b64ToText(b){return new TextDecoder('utf-8').decode(b64ToBytes(b));}
function makeJsBlobURL(t){return URL.createObjectURL(new Blob([t],{type:'application/javascript'}));}

var statusEl=document.getElementById('status');
var pagesEl=document.getElementById('pages');
function fail(msg){statusEl.textContent=msg;statusEl.style.display='block';}

var workerURL;
try{workerURL=makeJsBlobURL(b64ToText(WORKER_B64));}catch(e){fail('Worker init error: '+e.message);return;}

function start(pdfjsLib){
  if(!pdfjsLib){fail('pdfjsLib unavailable');return;}
  if(!USE_CANVAS&&typeof pdfjsLib.SVGGraphics!=='function'){fail('SVGGraphics unavailable in this PDF.js build');return;}

  if(USE_MODULE){
    try{pdfjsLib.GlobalWorkerOptions.workerPort=new Worker(workerURL,{type:'module'});}
    catch(e){pdfjsLib.GlobalWorkerOptions.workerSrc=workerURL;}
  }else{
    pdfjsLib.GlobalWorkerOptions.workerSrc=workerURL;
  }

  var bytes;
  try{bytes=b64ToBytes(PDF_B64);}catch(e){fail('PDF decode error: '+e.message);return;}

  pdfjsLib.getDocument({data:bytes}).promise.then(function(pdf){
    statusEl.textContent='Rendering '+pdf.numPages+' page(s)…';
    var maxCssWidth=Math.max(1,window.innerWidth-16);
    var chain=Promise.resolve();
    for(var n=1;n<=pdf.numPages;n++){(function(pageNum){
      chain=chain.then(function(){return pdf.getPage(pageNum);}).then(function(page){
        return USE_CANVAS?renderPageAsCanvas(page,maxCssWidth):renderPageAsSvg(page,maxCssWidth);
      });
    })(n);}
    return chain.then(function(){statusEl.style.display='none';});
  }).catch(function(err){fail('PDF load error: '+(err&&err.message||err));});

  // ベクター描画 (拡大時もピクセル化しない)。getSVG に渡す viewport は scale=1 にし、
  // SVG の viewBox を保ったまま CSS 側で寸法指定する方が拡大時に最も鮮明。
  function renderPageAsSvg(page,maxCssWidth){
    var base=page.getViewport({scale:1});
    var cssScale=Math.min(2.5,maxCssWidth/base.width);
    return page.getOperatorList().then(function(opList){
      var gfx=new pdfjsLib.SVGGraphics(page.commonObjs,page.objs);
      return gfx.getSVG(opList,base);
    }).then(function(svg){
      svg.setAttribute('class','page');
      svg.setAttribute('width',Math.ceil(base.width*cssScale)+'px');
      svg.setAttribute('height',Math.ceil(base.height*cssScale)+'px');
      if(!svg.getAttribute('viewBox')){
        svg.setAttribute('viewBox','0 0 '+base.width+' '+base.height);
      }
      pagesEl.appendChild(svg);
    });
  }

  // canvas 描画。Retina で滲まないよう devicePixelRatio 分だけ高解像度で描き、
  // CSS では論理ピクセルにサイズを戻す。
  function renderPageAsCanvas(page,maxCssWidth){
    var base=page.getViewport({scale:1});
    var cssScale=Math.min(2.5,maxCssWidth/base.width);
    var dpr=window.devicePixelRatio||1;
    var vp=page.getViewport({scale:cssScale*dpr});
    var canvas=document.createElement('canvas');
    canvas.className='page';
    canvas.width=Math.ceil(vp.width);
    canvas.height=Math.ceil(vp.height);
    canvas.style.width=Math.ceil(vp.width/dpr)+'px';
    canvas.style.height=Math.ceil(vp.height/dpr)+'px';
    var ctx=canvas.getContext('2d');
    return page.render({canvasContext:ctx,viewport:vp}).promise.then(function(){
      pagesEl.appendChild(canvas);
    });
  }
}

if(USE_MODULE){
  var mainURL;
  try{mainURL=makeJsBlobURL(b64ToText(MAIN_B64));}catch(e){fail('Library init error: '+e.message);return;}
  window.__trvisStartPdf=function(lib){start(lib);};
  var ms=document.createElement('script');
  ms.type='module';
  ms.textContent="import('"+mainURL+"').then(function(m){window.__trvisStartPdf(m);}).catch(function(e){document.getElementById('status').textContent='Failed to load PDF.js: '+(e&&e.message||e);});";
  ms.onerror=function(){fail('Failed to load PDF.js');};
  document.head.appendChild(ms);
}else{
  var mainScript=document.createElement('script');
  try{mainScript.src=makeJsBlobURL(b64ToText(MAIN_B64));}catch(e){fail('Library init error: '+e.message);return;}
  mainScript.onerror=function(){fail('Failed to load PDF.js');};
  mainScript.onload=function(){
    if(typeof pdfjsLib==='undefined'){fail('pdfjsLib unavailable');return;}
    start(pdfjsLib);
  };
  document.head.appendChild(mainScript);
}
})();
""";
}
