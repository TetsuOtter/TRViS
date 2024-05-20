using System.Net;
using TRViS.IO.RequestInfo;

namespace TRViS.IO;

public class OpenFile(HttpClient httpClient)
{
	public delegate Task<bool> CanContinueWhenResourceUriContainsIpDelegate(IPAddress ip, CancellationToken token);
	public delegate Task<bool> CanContinueWhenHeadRequestSuccessDelegate(HttpResponseMessage response, CancellationToken token);

	public CanContinueWhenHeadRequestSuccessDelegate? CanContinueWhenHeadRequestSuccess { get; set; } = null;
	public CanContinueWhenResourceUriContainsIpDelegate? CanContinueWhenResourceUriContainsIp { get; set; } = null;
	private readonly HttpClient HttpClient = httpClient;

	public Task<ILoader> OpenAppLinkAsync(
		string appLink,
		CancellationToken token
	)
	{
		AppLinkInfo appLinkInfo = AppLinkInfo.FromAppLink(appLink);
		return OpenAppLinkAsync(
			appLinkInfo,
			token
		);
	}

	public Task<ILoader> OpenAppLinkAsync(
		AppLinkInfo appLinkInfo,
		CancellationToken token
	)
	{
		try {
			if (appLinkInfo.ResourceUri is not null) {
				return OpenAppLink_PathTypeAsync(
					appLinkInfo,
					token
				);
			}
		} catch (Exception e) {
			// Contentがセットされている場合は、Contentでの処理を試みる
			// (例外が握りつぶされてしまうため、そこは何とかしたい)
			if (appLinkInfo.Content is null || appLinkInfo.Content.Length == 0) {
				return Task.FromException<ILoader>(e);
			}
		}

		if (appLinkInfo.Content is not null) {
			return Task.FromResult(OpenAppLink_DataType(appLinkInfo, token));
		}

		throw new ArgumentException("ResourceUri and Content are null");
	}

	private async Task<ILoader> OpenAppLink_PathTypeAsync(
		AppLinkInfo appLinkInfo,
		CancellationToken token
	)
	{
		if (appLinkInfo.ResourceUri is null) {
			throw new ArgumentException("ResourceUri is null");
		}

		token.ThrowIfCancellationRequested();

		Uri uri = appLinkInfo.ResourceUri;
		return uri.Scheme switch
		{
			"file" => appLinkInfo.FileTypeInfo switch
			{
				AppLinkInfo.FileType.Json => await LoaderJson.InitFromFileAsync(uri.LocalPath, token),
				AppLinkInfo.FileType.Sqlite => new LoaderSQL(uri.LocalPath),
				_ => throw new ArgumentException("Unknown file type"),
			},
			"http" or "https" => await OpenAppLink_HttpTypeAsync(
				appLinkInfo,
				uri,
				token
			),
			_ => throw new ArgumentException("Unknown scheme"),
		};
	}

	private static ILoader OpenAppLink_DataType(
		AppLinkInfo appLinkInfo,
		CancellationToken token
	)
	{
		if (appLinkInfo.Content is null || appLinkInfo.Content.Length == 0) {
			throw new ArgumentException("Content is null or empty");
		}

		if (appLinkInfo.FileTypeInfo != AppLinkInfo.FileType.Json) {
			throw new ArgumentException("This file type is not supported");
		}

		token.ThrowIfCancellationRequested();

		return LoaderJson.InitFromBytes(appLinkInfo.Content);
	}

	private async Task<ILoader> OpenAppLink_HttpTypeAsync(
		AppLinkInfo appLinkInfo,
		Uri uri,
		CancellationToken token
	)
	{
		if (appLinkInfo.FileTypeInfo != AppLinkInfo.FileType.Json) {
			throw new ArgumentException("This file type is not supported");
		}

		bool isHostIp = uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6;
		if (isHostIp
			&& this.CanContinueWhenResourceUriContainsIp is not null
			&& IPAddress.TryParse(uri.Host, out IPAddress? ip)
			&& !await this.CanContinueWhenResourceUriContainsIp(ip, token)) {
			throw new OperationCanceledException("cancelled by CanContinueWhenResourceUriContainsIp");
		}

		token.ThrowIfCancellationRequested();

		{
			using HttpRequestMessage request = new(HttpMethod.Head, uri);
			using HttpResponseMessage result = await this.HttpClient.SendAsync(request, token);

			if (!result.IsSuccessStatusCode) {
				throw new HttpRequestException(
					$"HEAD request to {uri} failed with code: {result.StatusCode}",
					inner: null,
					statusCode: result.StatusCode
				);
			}

			if (this.CanContinueWhenHeadRequestSuccess is not null
				&& !await this.CanContinueWhenHeadRequestSuccess(result, token)) {
				throw new OperationCanceledException("cancelled by CanContinueWhenHeadRequestSuccess");
			}
		}

		{
			using HttpRequestMessage request = new(HttpMethod.Get, uri);
			using HttpResponseMessage result = await this.HttpClient.SendAsync(request, token);
			if (!result.IsSuccessStatusCode)
			{
				throw new HttpRequestException(
					$"GET request to {uri} failed with code: {result.StatusCode}",
					inner: null,
					statusCode: result.StatusCode
				);
			}

			await using Stream stream = result.Content.ReadAsStream(token);
			// メソッドの先頭でJSONかチェックしているため、ここにはJSONしか来ない
			ILoader loader = await LoaderJson.InitFromStreamAsync(stream, token);

			return loader;
		}
	}
}
