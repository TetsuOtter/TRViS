using System.Collections.Generic;

using TRViS.NetworkSyncService;

namespace TRViS.Services;

/// <summary>
/// 受信した通告 (<see cref="NotificationData"/>) のクライアント側状態を保持する。
/// 重複排除 (<see cref="NotificationData.Id"/> 単位) と既読 (受領済み) 管理を行う、
/// MAUI 非依存の純粋ロジック。WebSocket 受信スレッドと UI スレッドから触られ得るため
/// スレッドセーフ。
/// <para>
/// 既読状態はメモリ上のみで、アプリ再起動では保持しない。サーバーは再配信時に
/// <see cref="NotificationData.Acknowledged"/> = true を付けることで、受領済みの通告を
/// 既読として復元できる (プロトコル参照)。
/// </para>
/// </summary>
public sealed class NotificationStore
{
	/// <summary>
	/// ストアが保持する 1 件の通告。表示用モデルも兼ねる。
	/// </summary>
	public sealed class Entry
	{
		/// <summary>元の通告データ。</summary>
		public NotificationData Data { get; }
		public string? Id => Data.Id;
		public string? Title => Data.Title;
		public string? Body => Data.Body;
		public int Priority => Data.Priority;
		public System.DateTimeOffset? IssuedAt => Data.IssuedAt;

		/// <summary>既読 (受領済み) か。</summary>
		public bool IsRead { get; internal set; }

		/// <summary>
		/// サーバーへ受領を送れるか。<see cref="NotificationData.Id"/> を持つ通告のみ受領できる。
		/// </summary>
		public bool CanAcknowledge => !string.IsNullOrEmpty(Data.Id);

		/// <summary>強調表示対象か (Priority 1 以上を重要とみなす)。</summary>
		public bool IsImportant => Data.Priority >= 1;

		internal Entry(NotificationData data, bool isRead)
		{
			Data = data;
			IsRead = isRead;
		}
	}

	/// <summary><see cref="Add"/> の結果。</summary>
	public readonly struct AddResult
	{
		/// <summary>ストアに反映された通告エントリ。</summary>
		public Entry Entry { get; init; }

		/// <summary>
		/// この通告をユーザーにポップアップ表示すべきか。
		/// 未読かつ「このセッションで初めて受信した通告」のときのみ true。
		/// </summary>
		public bool ShouldDisplay { get; init; }
	}

	private readonly object _lock = new();
	private readonly Dictionary<string, Entry> _byId = [];

	/// <summary>
	/// 通告を受信したときにストアへ反映し、ポップアップ表示すべきかを返す。
	/// </summary>
	/// <remarks>
	/// 表示判定ルール:
	/// <list type="bullet">
	/// <item>Id 付きの新規通告 → <see cref="NotificationData.Acknowledged"/> が false なら表示。</item>
	/// <item>既知の Id (再受信) → 内容は最新に更新するが再表示はしない (同一セッションでの重複ポップアップ抑止)。
	///   ただしサーバーが Acknowledged=true を付けてきたら既読へ昇格する。</item>
	/// <item>Id 無しの通告 → 重複排除・既読管理ができないため、Acknowledged が false なら都度表示。</item>
	/// </list>
	/// アプリ再起動でストアは空になるため、サーバーが Acknowledged 付きで再配信すれば
	/// 既読の通告は表示されず、未読の通告のみ再表示される。
	/// </remarks>
	public AddResult Add(NotificationData n)
	{
		lock (_lock)
		{
			string? id = n.Id;

			// Id 無し: 重複排除・既読管理不可。サーバーが受領済みと示していなければ都度表示する。
			if (string.IsNullOrEmpty(id))
			{
				var transient = new Entry(n, isRead: n.Acknowledged);
				return new AddResult { Entry = transient, ShouldDisplay = !transient.IsRead };
			}

			// 既知の Id: 内容を最新へ差し替えつつ既読状態は維持 (サーバー Acknowledged で昇格)。
			// 同一セッションで一度表示済みなので再表示はしない。
			if (_byId.TryGetValue(id, out var existing))
			{
				var updated = new Entry(n, isRead: existing.IsRead || n.Acknowledged);
				_byId[id] = updated;
				return new AddResult { Entry = updated, ShouldDisplay = false };
			}

			// 新規の Id。
			var entry = new Entry(n, isRead: n.Acknowledged);
			_byId[id] = entry;
			return new AddResult { Entry = entry, ShouldDisplay = !entry.IsRead };
		}
	}

	/// <summary>指定 Id の通告を既読 (受領済み) にする。存在しなければ何もしない。</summary>
	public void MarkRead(string id)
	{
		if (string.IsNullOrEmpty(id))
			return;
		lock (_lock)
		{
			if (_byId.TryGetValue(id, out var e))
				e.IsRead = true;
		}
	}

	/// <summary>指定 Id の通告が既読かを返す。未知の Id は false。</summary>
	public bool IsRead(string id)
	{
		if (string.IsNullOrEmpty(id))
			return false;
		lock (_lock)
		{
			return _byId.TryGetValue(id, out var e) && e.IsRead;
		}
	}

	/// <summary>保持している通告 (Id 付き) の件数。テスト用。</summary>
	public int Count
	{
		get { lock (_lock) { return _byId.Count; } }
	}

	/// <summary>保持している通告・既読状態をすべて破棄する。</summary>
	public void Clear()
	{
		lock (_lock)
		{
			_byId.Clear();
		}
	}
}
