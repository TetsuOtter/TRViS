using System.Net;

using NUnit.Framework;

using TRViS.NetworkSyncService;

namespace TRViS.NetworkSyncService.IntegrationTests;

/// <summary>
/// 連携元 (ゲーム等) がまだシナリオ/列車を読み込んでいないとき、TRViS.LocalServers の
/// 同期エンドポイントは 204 No Content や空ボディを返す。その際に
/// <see cref="HttpNetworkSyncService.GetSyncedDataAsync"/> が例外を投げると
/// NetworkSyncServiceTask が規定回数で GPS 測位へフォールバックし、時計が止まり
/// 位置情報も更新されなくなる (HTTP 連携で報告された不具合)。
/// 204/空ボディを一時的状態として握りつぶし、ポーリングを継続することを確認する。
/// </summary>
[TestFixture]
public class HttpSyncDataNoContentTests
{
	private sealed class StubHandler : HttpMessageHandler
	{
		public Func<HttpResponseMessage> ResponseFactory { get; set; } =
			() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> Task.FromResult(ResponseFactory());
	}

	private static HttpNetworkSyncService MakeService(StubHandler handler)
		=> new(new Uri("http://localhost/syncdata.json"), new HttpClient(handler));

	[Test]
	public void TickAsync_204NoContent_DoesNotThrow_AndKeepsPolling()
	{
		var handler = new StubHandler
		{
			ResponseFactory = () => new HttpResponseMessage(HttpStatusCode.NoContent),
		};
		using var service = MakeService(handler);

		Assert.DoesNotThrowAsync(async () =>
		{
			await service.TickAsync();
			await service.TickAsync();
		});
		Assert.That(service.CanStart, Is.False, "204 should be treated as a benign no-data state");
	}

	[Test]
	public void TickAsync_200EmptyBody_DoesNotThrow()
	{
		var handler = new StubHandler
		{
			ResponseFactory = () => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(string.Empty),
			},
		};
		using var service = MakeService(handler);

		Assert.DoesNotThrowAsync(async () => await service.TickAsync());
		Assert.That(service.CanStart, Is.False);
	}

	[Test]
	public async Task TickAsync_RecoversToRealDataAfterNoContent()
	{
		bool serveData = false;
		var handler = new StubHandler
		{
			ResponseFactory = () => serveData
				? new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("{\"Location_m\":null,\"Time_ms\":43200000,\"CanStart\":true}"),
				}
				: new HttpResponseMessage(HttpStatusCode.NoContent),
		};
		using var service = MakeService(handler);

		int? lastTime = null;
		service.TimeChanged += (_, t) => lastTime = t;

		await service.TickAsync(); // 204 -> benign, no throw
		Assert.That(service.CanStart, Is.False);

		serveData = true;
		await service.TickAsync(); // now real data flows through

		Assert.That(service.CanStart, Is.True);
		Assert.That(lastTime, Is.EqualTo(43_200));
	}
}
