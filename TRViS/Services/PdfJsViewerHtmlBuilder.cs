using System.Text;

namespace TRViS.Services;

/// <summary>
/// PDF.js を内蔵した自己完結型 HTML を生成する。
/// 全プラットフォームで同一の WebView レンダリング経路を実現するため、
/// pdf.js / pdf.worker は base64 で埋め込み、Blob URL 経由で実行させる。
/// </summary>
internal static class PdfJsViewerHtmlBuilder
{
	private const string PdfJsMainAssetPath = "pdfjs/pdf.min.js";
	private const string PdfJsWorkerAssetPath = "pdfjs/pdf.worker.min.js";

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private static string? _cachedMainBase64;
	private static string? _cachedWorkerBase64;
	private static readonly SemaphoreSlim _loadLock = new(1, 1);

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
		if (_cachedMainBase64 is not null && _cachedWorkerBase64 is not null)
			return (_cachedMainBase64, _cachedWorkerBase64);

		await _loadLock.WaitAsync().ConfigureAwait(false);
		try
		{
			if (_cachedMainBase64 is null)
				_cachedMainBase64 = await ReadAssetAsBase64Async(PdfJsMainAssetPath).ConfigureAwait(false);
			if (_cachedWorkerBase64 is null)
				_cachedWorkerBase64 = await ReadAssetAsBase64Async(PdfJsWorkerAssetPath).ConfigureAwait(false);
			return (_cachedMainBase64, _cachedWorkerBase64);
		}
		finally
		{
			_loadLock.Release();
		}
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
		var sb = new StringBuilder(capacity: mainBase64.Length + workerBase64.Length + pdfBase64.Length + 4096);

		sb.Append("<!DOCTYPE html>\n");
		sb.Append("<html>\n<head>\n");
		sb.Append("<meta charset=\"utf-8\">\n");
		sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, user-scalable=yes\">\n");
		sb.Append("<style>\n");
		sb.Append("html,body{margin:0;padding:0;background:#525659;}\n");
		sb.Append("#pages{display:flex;flex-direction:column;align-items:center;padding:8px 0;gap:8px;}\n");
		sb.Append(".page{background:#fff;box-shadow:0 2px 8px rgba(0,0,0,0.3);max-width:100%;height:auto;}\n");
		sb.Append("#status{color:#ddd;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;padding:16px;text-align:center;}\n");
		sb.Append("</style>\n</head>\n<body>\n");
		sb.Append("<div id=\"status\">Loading PDF…</div>\n");
		sb.Append("<div id=\"pages\"></div>\n");

		sb.Append("<script>\n");
		sb.Append("(function(){\n");
		sb.Append("var MAIN_B64='").Append(mainBase64).Append("';\n");
		sb.Append("var WORKER_B64='").Append(workerBase64).Append("';\n");
		sb.Append("var PDF_B64='").Append(pdfBase64).Append("';\n");

		sb.Append("function b64ToBytes(b){var s=atob(b);var u=new Uint8Array(s.length);for(var i=0;i<s.length;i++)u[i]=s.charCodeAt(i);return u;}\n");
		sb.Append("function b64ToText(b){return new TextDecoder('utf-8').decode(b64ToBytes(b));}\n");
		sb.Append("function makeJsBlobURL(t){return URL.createObjectURL(new Blob([t],{type:'application/javascript'}));}\n");

		sb.Append("var statusEl=document.getElementById('status');\n");
		sb.Append("var pagesEl=document.getElementById('pages');\n");
		sb.Append("function fail(msg){statusEl.textContent=msg;statusEl.style.display='block';}\n");

		sb.Append("var workerURL;\n");
		sb.Append("try{workerURL=makeJsBlobURL(b64ToText(WORKER_B64));}catch(e){fail('Worker init error: '+e.message);return;}\n");

		sb.Append("var mainScript=document.createElement('script');\n");
		sb.Append("try{mainScript.src=makeJsBlobURL(b64ToText(MAIN_B64));}catch(e){fail('Library init error: '+e.message);return;}\n");
		sb.Append("mainScript.onerror=function(){fail('Failed to load PDF.js');};\n");
		sb.Append("mainScript.onload=function(){\n");
		sb.Append("  if(typeof pdfjsLib==='undefined'){fail('pdfjsLib unavailable');return;}\n");
		sb.Append("  pdfjsLib.GlobalWorkerOptions.workerSrc=workerURL;\n");
		sb.Append("  var bytes;\n");
		sb.Append("  try{bytes=b64ToBytes(PDF_B64);}catch(e){fail('PDF decode error: '+e.message);return;}\n");
		sb.Append("  pdfjsLib.getDocument({data:bytes}).promise.then(function(pdf){\n");
		sb.Append("    statusEl.textContent='Rendering '+pdf.numPages+' page(s)…';\n");
		sb.Append("    var chain=Promise.resolve();\n");
		sb.Append("    var dpr=window.devicePixelRatio||1;\n");
		sb.Append("    var maxCssWidth=Math.max(1,window.innerWidth-16);\n");
		sb.Append("    for(var n=1;n<=pdf.numPages;n++){(function(pageNum){\n");
		sb.Append("      chain=chain.then(function(){return pdf.getPage(pageNum);}).then(function(page){\n");
		sb.Append("        var base=page.getViewport({scale:1});\n");
		sb.Append("        var cssScale=Math.min(2.5,maxCssWidth/base.width);\n");
		sb.Append("        var renderScale=cssScale*dpr;\n");
		sb.Append("        var vp=page.getViewport({scale:renderScale});\n");
		sb.Append("        var canvas=document.createElement('canvas');\n");
		sb.Append("        canvas.className='page';\n");
		sb.Append("        canvas.width=Math.ceil(vp.width);canvas.height=Math.ceil(vp.height);\n");
		sb.Append("        canvas.style.width=Math.ceil(vp.width/dpr)+'px';\n");
		sb.Append("        canvas.style.height=Math.ceil(vp.height/dpr)+'px';\n");
		sb.Append("        pagesEl.appendChild(canvas);\n");
		sb.Append("        return page.render({canvasContext:canvas.getContext('2d'),viewport:vp}).promise;\n");
		sb.Append("      });\n");
		sb.Append("    })(n);}\n");
		sb.Append("    return chain.then(function(){statusEl.style.display='none';});\n");
		sb.Append("  }).catch(function(err){fail('PDF load error: '+(err&&err.message||err));});\n");
		sb.Append("};\n");
		sb.Append("document.head.appendChild(mainScript);\n");
		sb.Append("})();\n");
		sb.Append("</script>\n");
		sb.Append("</body>\n</html>");

		return sb.ToString();
	}
}
