using System;

namespace TRViS.NetworkSyncService;

/// <summary>
/// 1 つの <see cref="NetworkSyncServiceBase"/> の接続断 (
/// <see cref="NetworkSyncServiceBase.ConnectionClosed"/> /
/// <see cref="NetworkSyncServiceBase.ConnectionFailed"/>) を監視し、
/// 「現在監視しているサービス」が接続を失ったときだけコールバックを呼ぶ。
///
/// <para>
/// 別サービスを <see cref="Watch"/> し直すと旧サービスのハンドラを必ず外すため、
/// 再接続で差し替えられた旧インスタンスの遅延イベントがコールバックを
/// 再発火させることはない (#261)。AppViewModel 側はこのコールバックで
/// UI スレッドへマーシャリングして観測対象プロパティを更新する想定で、
/// このクラス自体は MAUI 非依存・同期で単体テスト可能。
/// </para>
/// </summary>
public sealed class NetworkSyncConnectionLostWatcher
{
	private readonly Action _onConnectionLost;
	private NetworkSyncServiceBase? _watched;

	public NetworkSyncConnectionLostWatcher(Action onConnectionLost)
	{
		ArgumentNullException.ThrowIfNull(onConnectionLost);
		_onConnectionLost = onConnectionLost;
	}

	/// <summary>現在監視中のサービス。未監視なら null。</summary>
	public NetworkSyncServiceBase? Watched => _watched;

	/// <summary>
	/// <paramref name="service"/> の接続断監視を開始する。既に別サービスを
	/// 監視していた場合はそのハンドラを先に外す。同一インスタンスの再 Watch は
	/// 二重購読しないよう no-op。
	/// </summary>
	public void Watch(NetworkSyncServiceBase service)
	{
		ArgumentNullException.ThrowIfNull(service);
		if (ReferenceEquals(_watched, service))
			return;
		Detach();
		_watched = service;
		service.ConnectionClosed += OnConnectionLost;
		service.ConnectionFailed += OnConnectionLost;
	}

	/// <summary>監視を解除する。以後どのサービスのイベントでもコールバックは呼ばれない。</summary>
	public void Clear() => Detach();

	private void Detach()
	{
		if (_watched is null)
			return;
		_watched.ConnectionClosed -= OnConnectionLost;
		_watched.ConnectionFailed -= OnConnectionLost;
		_watched = null;
	}

	private void OnConnectionLost(object? sender, EventArgs e)
	{
		// 既に差し替え/解除済みのサービスからの遅延イベントは無視する。
		if (!ReferenceEquals(sender, _watched))
			return;
		_onConnectionLost();
	}
}
