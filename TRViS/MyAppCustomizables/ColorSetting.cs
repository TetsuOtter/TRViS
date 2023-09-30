namespace TRViS.MyAppCustomizables;

/// <summary>
/// 色情報の設定
/// </summary>
/// <param name="red">赤色成分</param>
/// <param name="green">緑色成分</param>
/// <param name="blue">青色成分</param>
public class ColorSetting(byte red, byte green, byte blue)
{
	/// <summary>
	/// 赤色成分
	/// </summary>
	public byte Red { get; set; } = red;
	/// <summary>
	/// 緑色成分
	/// </summary>
	public byte Green { get; set; } = green;
	/// <summary>
	/// 青色成分
	/// </summary>
	public byte Blue { get; set; } = blue;

	/// <summary>
	/// 色情報を黒で初期化する
	/// </summary>
	public ColorSetting() : this(0, 0, 0) { }

	/// <summary>
	/// 色情報を指定したMAUIColorで初期化する
	/// </summary>
	/// <param name="color">色情報</param>
	public ColorSetting(Color color) : this(
		(byte)(color.Red * 255.99999),
		(byte)(color.Green * 255.99999),
		(byte)(color.Blue * 255.99999)
	) { }

	/// <summary>
	/// 色情報を指定したMAUIColorに変換する
	/// </summary>
	/// <returns>MAUI Color</returns>
	public Color ToColor()
		=> new(Red, Green, Blue);

	/// <summary>
	/// 色情報をHEX文字列化する (#RRGGBB形式)
	/// </summary>
	/// <returns>色情報文字列</returns>
	public override string ToString()
	{
		return $"#{Red:X2}{Green:X2}{Blue:X2}";
	}

	/// <summary>
	/// 指定した色とこの色情報が等しいかどうかを判断する
	/// </summary>
	/// <param name="other">比較相手</param>
	/// <returns>等しいかどうか</returns>
	public bool Equals(ColorSetting? other)
	{
		if (other is null)
			return false;

		return (
			Red == other.Red
			&& Green == other.Green
			&& Blue == other.Blue
		);
	}
	/// <summary>
	/// 指定した色とこの色情報が等しいかどうかを判断する
	/// </summary>
	/// <param name="other">比較相手</param>
	/// <returns>等しいかどうか</returns>
	public override bool Equals(object? obj)
	{
		return this.Equals(obj as ColorSetting);
	}

	/// <summary>
	/// ハッシュコードを取得する
	/// </summary>
	/// <returns>HashCode</returns>
	public override int GetHashCode()
	{
		return (
			(Red << 16)
			| (Green << 8)
			| Blue
		);
	}
}
