using System.Collections.Concurrent;

namespace TRViS.Services;

/// <summary>
/// PDF.js を内蔵した自己完結型 HTML を生成する。
/// 全プラットフォームで同一の WebView レンダリング経路を実現するため、
/// pdf.js / pdf.worker は base64 で埋め込み、Blob URL 経由で実行させる。
/// ベクター描画 (SVGGraphics) を採用しており、ピンチズーム時もピクセル化しない。
/// </summary>
internal static class PdfJsViewerHtmlBuilder
{
	private const string ModernMainAssetPath = "pdfjs/pdf.min.js";
	private const string ModernWorkerAssetPath = "pdfjs/pdf.worker.min.js";

	private const string MainPlaceholder = "__PDFJS_MAIN_B64__";
	private const string WorkerPlaceholder = "__PDFJS_WORKER_B64__";
	private const string PdfPlaceholder = "__PDF_B64__";

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private static readonly ConcurrentDictionary<string, Task<string>> _assetBase64Cache = new();

	/// <summary>
	/// PDF バイト列 (base64 文字列) を受け取り、PDF.js で全ページをレンダリングする
	/// 自己完結型 HTML を返す。pdf.js のスクリプト本体はキャッシュされる。
	/// </summary>
	/// <param name="pdfBase64">PDF バイナリの base64 表現 (Formatter から受け取る Payload)。</param>
	/// <returns>WebView.Source = HtmlWebViewSource{Html=…} に流せる HTML 文字列。</returns>
	public static async Task<string> BuildAsync(string pdfBase64)
	{
		var (mainB64, workerB64) = await LoadScriptsAsync().ConfigureAwait(false);
		return BuildHtml(pdfBase64, mainB64, workerB64);
	}

	private static async Task<(string MainBase64, string WorkerBase64)> LoadScriptsAsync()
	{
		var mainTask = _assetBase64Cache.GetOrAdd(ModernMainAssetPath, ReadAssetAsBase64Async);
		var workerTask = _assetBase64Cache.GetOrAdd(ModernWorkerAssetPath, ReadAssetAsBase64Async);

		return (await mainTask.ConfigureAwait(false), await workerTask.ConfigureAwait(false));
	}

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

	private static string BuildHtml(string pdfBase64, string mainBase64, string workerBase64)
	{
		string js = ViewerJsTemplate
			.Replace(MainPlaceholder, mainBase64)
			.Replace(WorkerPlaceholder, workerBase64)
			.Replace(PdfPlaceholder, pdfBase64);

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

	// JS は ES5 互換 (var / function 宣言) で書く。iOS 12 WebView 含め広い互換性を保つ。
	// PDF は pdfjsLib.SVGGraphics でベクター描画する (拡大時もピクセル化しない)。
	private const string ViewerJsTemplate = """
(function(){
var MAIN_B64='__PDFJS_MAIN_B64__';
var WORKER_B64='__PDFJS_WORKER_B64__';
var PDF_B64='__PDF_B64__';

function b64ToBytes(b){var s=atob(b);var u=new Uint8Array(s.length);for(var i=0;i<s.length;i++)u[i]=s.charCodeAt(i);return u;}
function b64ToText(b){return new TextDecoder('utf-8').decode(b64ToBytes(b));}
function makeJsBlobURL(t){return URL.createObjectURL(new Blob([t],{type:'application/javascript'}));}

var statusEl=document.getElementById('status');
var pagesEl=document.getElementById('pages');
function fail(msg){statusEl.textContent=msg;statusEl.style.display='block';}

var workerURL;
try{workerURL=makeJsBlobURL(b64ToText(WORKER_B64));}catch(e){fail('Worker init error: '+e.message);return;}

var mainScript=document.createElement('script');
try{mainScript.src=makeJsBlobURL(b64ToText(MAIN_B64));}catch(e){fail('Library init error: '+e.message);return;}
mainScript.onerror=function(){fail('Failed to load PDF.js');};
mainScript.onload=function(){
  if(typeof pdfjsLib==='undefined'){fail('pdfjsLib unavailable');return;}
  if(typeof pdfjsLib.SVGGraphics!=='function'){fail('SVGGraphics unavailable in this PDF.js build');return;}
  pdfjsLib.GlobalWorkerOptions.workerSrc=workerURL;

  var bytes;
  try{bytes=b64ToBytes(PDF_B64);}catch(e){fail('PDF decode error: '+e.message);return;}

  pdfjsLib.getDocument({data:bytes}).promise.then(function(pdf){
    statusEl.textContent='Rendering '+pdf.numPages+' page(s)…';
    var maxCssWidth=Math.max(1,window.innerWidth-16);
    var chain=Promise.resolve();
    for(var n=1;n<=pdf.numPages;n++){(function(pageNum){
      chain=chain.then(function(){return pdf.getPage(pageNum);}).then(function(page){
        return renderPageAsSvg(page,maxCssWidth);
      });
    })(n);}
    return chain.then(function(){statusEl.style.display='none';});
  }).catch(function(err){fail('PDF load error: '+(err&&err.message||err));});

  function renderPageAsSvg(page,maxCssWidth){
    var base=page.getViewport({scale:1});
    // 表示倍率 (CSS px ベース)。getSVG に渡す viewport 自体は scale=1 にして
    // SVG の viewBox を保ったまま CSS 側で寸法指定する方が拡大時に最も鮮明。
    var cssScale=Math.min(2.5,maxCssWidth/base.width);
    return page.getOperatorList().then(function(opList){
      var gfx=new pdfjsLib.SVGGraphics(page.commonObjs,page.objs);
      return gfx.getSVG(opList,base);
    }).then(function(svg){
      svg.setAttribute('class','page');
      svg.setAttribute('width',Math.ceil(base.width*cssScale)+'px');
      svg.setAttribute('height',Math.ceil(base.height*cssScale)+'px');
      // viewBox を持たない場合は付与 (ベクターのままズームできるように)
      if(!svg.getAttribute('viewBox')){
        svg.setAttribute('viewBox','0 0 '+base.width+' '+base.height);
      }
      pagesEl.appendChild(svg);
    });
  }
};
document.head.appendChild(mainScript);
})();
""";
}
