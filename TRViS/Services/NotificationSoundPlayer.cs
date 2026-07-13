using NLog;

using Plugin.Maui.Audio;

using TRViS.Core;

namespace TRViS.Services;

/// <summary>
/// 通告の受信音・接近音 (<see cref="SoundRef"/>) を再生するサービス。音声バイナリは
/// 常にサーバーから送られてくる (アプリには同梱しない、#329)。デコード/再生の失敗は
/// すべてログのみに留め、例外を外へ漏らさない (副次的な UX 機能のため、失敗時は単に
/// 無音になるだけでよい)。同時に複数の音を重ねて再生しない (直前の再生が残っていれば
/// 停止してから新しい音を再生する)。
/// </summary>
public sealed class NotificationSoundPlayer : IDisposable
{
	private static readonly Logger logger = LoggerService.GetGeneralLogger();

	private readonly IAudioManager _audioManager;
	private IAudioPlayer? _currentPlayer;

	public NotificationSoundPlayer(IAudioManager? audioManager = null)
	{
		_audioManager = audioManager ?? AudioManager.Current;
	}

	/// <summary>
	/// <paramref name="sound"/> を再生する。<c>null</c> (無音) のときは何もしない。
	/// デコード失敗・サイズ超過・非対応形式はすべて無音として扱い、例外を投げない。
	/// </summary>
	public void Play(SoundRef? sound)
	{
		if (sound is null)
			return;

		byte[]? bytes = NotificationSoundDecoder.TryDecode(sound.DataBase64);
		if (bytes is null)
		{
			logger.Warn(
				"NotificationSoundPlayer: Failed to decode sound or exceeds {0} bytes (Format={1}), skipping",
				NotificationSoundDecoder.MaxDecodedBytes, sound.Format
			);
			return;
		}

		try
		{
			// スタックさせない: 直前の再生が残っていれば止めてから差し替える。
			_currentPlayer?.Stop();
			_currentPlayer?.Dispose();

			var player = _audioManager.CreatePlayer(new MemoryStream(bytes));
			_currentPlayer = player;
			player.Play();
		}
		catch (Exception ex)
		{
			// 非対応形式を含め、再生失敗はすべて無音扱い。
			logger.Warn(ex, "NotificationSoundPlayer: Failed to play sound (Format={0})", sound.Format);
		}
	}

	/// <summary>
	/// 再生中の通告音があれば停止する (画面タップによる停止用、#329)。呼び出し時点で実際に
	/// 再生中だったかどうかを返す — 呼び出し側 (バナーのタップ展開など) が、音を止めただけなのか
	/// 元々何も鳴っていなかったのかを区別できるようにするため。
	/// </summary>
	public bool StopIfPlaying()
	{
		if (_currentPlayer is not IAudioPlayer player || !player.IsPlaying)
			return false;

		player.Stop();
		return true;
	}

	public void Dispose()
	{
		_currentPlayer?.Stop();
		_currentPlayer?.Dispose();
		_currentPlayer = null;
	}
}
