using TRViS.NetworkSyncService;
using TRViS.Services;

namespace TRViS.LocationService.Tests;

/// <summary>
/// <see cref="NotificationStore"/> の重複排除・既読 (受領済み) 管理・表示判定を検証する。
/// </summary>
[TestFixture]
public class NotificationStoreTests
{
	private static NotificationData Make(
		string? id = "n-1",
		string? title = "タイトル",
		string? body = "本文",
		int priority = 0,
		bool acknowledged = false)
		=> new()
		{
			Id = id,
			Title = title,
			Body = body,
			Priority = priority,
			Acknowledged = acknowledged,
		};

	[Test]
	public void Add_EntryExposesDisplayFields()
	{
		var store = new NotificationStore();
		var data = new NotificationData
		{
			Id = "n-1",
			OrderNumber = "ORD-001",
			Title = "タイトル",
			Body = "本文",
			Receiver = "乗務員A",
			Sender = "指令所",
			IconText = "指",
			IconColor_RGB = 0xC62828,
			IconImageBase64 = "AAA=",
			IssuedAtIsUnspecifiedTimeZone = true,
		};

		var result = store.Add(data);

		Assert.Multiple(() =>
		{
			Assert.That(result.Entry.OrderNumber, Is.EqualTo("ORD-001"));
			Assert.That(result.Entry.Receiver, Is.EqualTo("乗務員A"));
			Assert.That(result.Entry.Sender, Is.EqualTo("指令所"));
			Assert.That(result.Entry.IconText, Is.EqualTo("指"));
			Assert.That(result.Entry.IconColor_RGB, Is.EqualTo(0xC62828));
			Assert.That(result.Entry.IconImageBase64, Is.EqualTo("AAA="));
			Assert.That(result.Entry.IssuedAtIsUnspecifiedTimeZone, Is.True);
		});
	}

	[Test]
	public void Add_NewUnread_ShouldDisplayAndTrack()
	{
		var store = new NotificationStore();

		var result = store.Add(Make(id: "n-1", acknowledged: false));

		Assert.Multiple(() =>
		{
			Assert.That(result.ShouldDisplay, Is.True);
			Assert.That(result.Entry.IsRead, Is.False);
			Assert.That(result.Entry.CanAcknowledge, Is.True);
			Assert.That(store.Count, Is.EqualTo(1));
			Assert.That(store.IsRead("n-1"), Is.False);
		});
	}

	[Test]
	public void Add_ServerMarkedAcknowledged_IsReadAndNotDisplayed()
	{
		var store = new NotificationStore();

		var result = store.Add(Make(id: "n-1", acknowledged: true));

		Assert.Multiple(() =>
		{
			Assert.That(result.ShouldDisplay, Is.False);
			Assert.That(result.Entry.IsRead, Is.True);
			Assert.That(store.IsRead("n-1"), Is.True);
		});
	}

	[Test]
	public void Add_DuplicateId_DoesNotRedisplay()
	{
		var store = new NotificationStore();
		store.Add(Make(id: "n-1", title: "旧", acknowledged: false));

		var second = store.Add(Make(id: "n-1", title: "新", acknowledged: false));

		Assert.Multiple(() =>
		{
			Assert.That(second.ShouldDisplay, Is.False, "同一セッションでの重複ポップアップは抑止する");
			Assert.That(second.Entry.Title, Is.EqualTo("新"), "内容は最新へ更新される");
			Assert.That(store.Count, Is.EqualTo(1));
		});
	}

	[Test]
	public void Add_DuplicateId_AfterServerAcknowledged_PromotesToRead()
	{
		var store = new NotificationStore();
		store.Add(Make(id: "n-1", acknowledged: false)); // 未読で受信

		var second = store.Add(Make(id: "n-1", acknowledged: true)); // 再配信で受領済みに

		Assert.Multiple(() =>
		{
			Assert.That(second.ShouldDisplay, Is.False);
			Assert.That(second.Entry.IsRead, Is.True);
			Assert.That(store.IsRead("n-1"), Is.True);
		});
	}

	[Test]
	public void MarkRead_ThenReReceive_StaysReadAndNotDisplayed()
	{
		var store = new NotificationStore();
		store.Add(Make(id: "n-1", acknowledged: false));

		store.MarkRead("n-1");
		var again = store.Add(Make(id: "n-1", acknowledged: false));

		Assert.Multiple(() =>
		{
			Assert.That(store.IsRead("n-1"), Is.True);
			Assert.That(again.ShouldDisplay, Is.False);
			Assert.That(again.Entry.IsRead, Is.True);
		});
	}

	[Test]
	public void Add_NullId_NotTrackedButDisplayedWhenUnread()
	{
		var store = new NotificationStore();

		var result = store.Add(Make(id: null, acknowledged: false));

		Assert.Multiple(() =>
		{
			Assert.That(result.ShouldDisplay, Is.True);
			Assert.That(result.Entry.CanAcknowledge, Is.False, "Id 無しは受領できない");
			Assert.That(store.Count, Is.EqualTo(0), "Id 無しは重複排除・既読管理の対象外");
		});
	}

	[Test]
	public void Add_NullId_ServerAcknowledged_NotDisplayed()
	{
		var store = new NotificationStore();

		var result = store.Add(Make(id: null, acknowledged: true));

		Assert.That(result.ShouldDisplay, Is.False);
	}

	[Test]
	public void Entry_ImportantReflectsPriority()
	{
		var store = new NotificationStore();

		var normal = store.Add(Make(id: "n-normal", priority: 0));
		var important = store.Add(Make(id: "n-important", priority: 1));

		Assert.Multiple(() =>
		{
			Assert.That(normal.Entry.IsImportant, Is.False);
			Assert.That(important.Entry.IsImportant, Is.True);
		});
	}

	[Test]
	public void MarkRead_UnknownId_DoesNotThrow()
	{
		var store = new NotificationStore();
		Assert.DoesNotThrow(() => store.MarkRead("nonexistent"));
		Assert.That(store.IsRead("nonexistent"), Is.False);
	}
}
