namespace TRViS.DTAC.HakoParts;

/// <summary>
/// Shared math for rendering a short vertical (tategaki-style) string as one Label per
/// character, each in its own <paramref name="charSlotHeight"/>-tall slot within a fixed
/// <paramref name="headerHeight"/> — used by both <see cref="HeaderView"/> ("乗務開始"/
/// "乗務終了") and <see cref="DiagramHeaderView"/> (turn-back station names) so the two headers,
/// which sit in the same-height row and must look consistent, share one definition instead of two
/// copies that could drift apart.
/// </summary>
static class VerticalCharacterLayout
{
	/// <summary>
	/// Computes each character's vertical center, matching a 4-character name's reference
	/// layout: <paramref name="charSlotHeight"/>-tall slots packed flush, centered as a group
	/// within <paramref name="headerHeight"/> (i.e. a fixed (headerHeight - 4 * charSlotHeight) /
	/// 2 margin above the first slot and below the last). A shorter name reuses that same
	/// first/last slot-center position instead of drifting toward the middle; characters in
	/// between are spread evenly (so a 3-character name's middle character lands exactly at the
	/// center). A 1-character name is centered within the full header height instead.
	/// </summary>
	public static IEnumerable<(char Character, double CenterY)> ComputePositions(string text, double headerHeight, double charSlotHeight)
	{
		if (text.Length == 0)
			yield break;

		if (text.Length == 1)
		{
			yield return (text[0], headerHeight / 2);
			yield break;
		}

		double margin = (headerHeight - (4 * charSlotHeight)) / 2;
		double firstCenterY = margin + (charSlotHeight / 2);
		double lastCenterY = headerHeight - margin - (charSlotHeight / 2);

		for (int i = 0; i < text.Length; i++)
		{
			double centerY = firstCenterY + i * (lastCenterY - firstCenterY) / (text.Length - 1);
			yield return (text[i], centerY);
		}
	}
}
