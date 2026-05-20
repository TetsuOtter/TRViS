using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TRViS.UITests.Infrastructure;

/// <summary>
/// Tolerance-based PNG pixel diff for the screenshot-regression gate.
///
/// A screenshot is compared against a committed baseline. Two knobs absorb
/// the unavoidable cross-run jitter of simulator rendering without hiding
/// real UI regressions:
///
///  * <c>channelTolerance</c> — max per-channel (R/G/B/A) absolute delta for
///    a pixel to count as "unchanged". Absorbs sub-pixel anti-aliasing and
///    PNG-recompression noise.
///  * <c>maxDiffFraction</c> — fraction of pixels allowed to exceed the
///    channel tolerance before the baseline is considered violated.
///    Absorbs caret blink / 1px layout rounding.
///
/// Every screen is gated in full: there is no per-region exclusion. The few
/// intrinsically non-deterministic elements (the app clock, the system
/// theme, the status bar, the machine-specific log-file path) are pinned to
/// fixed values by <c>UI_TEST</c> seams in the app, so the entire captured
/// viewport is deterministic and pixel-comparable.
///
/// On a mismatch the actual screenshot and a red-highlight diff image are
/// written into the artifact directory (the NUnit WorkDirectory, which the
/// CI workflow already uploads via the <c>**/TestResults/**/*.png</c> glob)
/// so failures are diagnosable from the run artifacts alone.
/// </summary>
public static class ScreenshotComparer
{
	// 16/255 ≈ 6%. Empirically absorbs simulator AA / PNG round-trip noise
	// while still catching colour/theme/layout regressions (which move whole
	// regions by far more than 16 levels).
	public const int DefaultChannelTolerance = 16;

	// 0.5% of the screen. A genuine layout/theme regression moves thousands
	// of pixels (well over 0.5%); caret blink / 1px text reflow stays under.
	public const double DefaultMaxDiffFraction = 0.005;

	public sealed record Result(bool Match, string Message);

	/// <summary>
	/// Compare <paramref name="actualPng"/> against the baseline at
	/// <paramref name="baselinePath"/>. When <paramref name="updateMode"/>
	/// is true the baseline is (over)written and the result is always a
	/// pass — this is the "regenerate baselines" path.
	/// </summary>
	public static Result CompareOrUpdate(
		byte[] actualPng,
		string baselinePath,
		string artifactDir,
		string artifactName,
		bool updateMode,
		int channelTolerance = DefaultChannelTolerance,
		double maxDiffFraction = DefaultMaxDiffFraction)
	{
		if (updateMode)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
			File.WriteAllBytes(baselinePath, actualPng);
			return new Result(true, $"[update] wrote baseline {baselinePath} ({actualPng.Length} B)");
		}

		if (!File.Exists(baselinePath))
		{
			WriteArtifact(artifactDir, artifactName + ".actual.png", actualPng);
			return new Result(false,
				$"No baseline at {baselinePath}. Wrote {artifactName}.actual.png — " +
				"run ./update-screenshots.sh (or set SCREENSHOT_UPDATE=1) to create it.");
		}

		using var actual = Image.Load<Rgba32>(actualPng);
		using var baseline = Image.Load<Rgba32>(baselinePath);

		if (actual.Width != baseline.Width || actual.Height != baseline.Height)
		{
			WriteArtifact(artifactDir, artifactName + ".actual.png", actualPng);
			return new Result(false,
				$"{artifactName}: size mismatch — baseline {baseline.Width}x{baseline.Height} " +
				$"vs actual {actual.Width}x{actual.Height}. Wrote {artifactName}.actual.png.");
		}

		int w = baseline.Width, h = baseline.Height;
		long n = (long)w * h;

		var b = new Rgba32[n];
		var a = new Rgba32[n];
		baseline.CopyPixelDataTo(b);
		actual.CopyPixelDataTo(a);

		// Pass 1: only count differing pixels. The diff PNG (w*h*4 bytes) is
		// allocated and filled lazily on the failure path so that the common
		// pass case — 80 captures per run — avoids that extra buffer.
		long differing = 0;
		for (long i = 0; i < n; i++)
		{
			var bp = b[i];
			var ap = a[i];
			int max = Math.Max(
				Math.Max(Math.Abs(bp.R - ap.R), Math.Abs(bp.G - ap.G)),
				Math.Max(Math.Abs(bp.B - ap.B), Math.Abs(bp.A - ap.A)));

			if (max > channelTolerance)
				differing++;
		}

		double fraction = n == 0 ? 0 : (double)differing / n;
		if (fraction > maxDiffFraction)
		{
			WriteArtifact(artifactDir, artifactName + ".actual.png", actualPng);
			WriteArtifact(artifactDir, artifactName + ".diff.png", BuildDiffPng(b, a, w, h, channelTolerance));
			return new Result(false,
				$"{artifactName}: {differing}/{n} px differ " +
				$"({fraction:P3} > {maxDiffFraction:P3} budget, channel delta > {channelTolerance}). " +
				$"Wrote {artifactName}.actual.png + {artifactName}.diff.png.");
		}

		return new Result(true,
			$"{artifactName}: OK — {differing}/{n} px differ " +
			$"({fraction:P3} <= {maxDiffFraction:P3}).");
	}

	private static byte[] BuildDiffPng(Rgba32[] b, Rgba32[] a, int w, int h, int channelTolerance)
	{
		long n = (long)w * h;
		var diff = new Rgba32[n];
		for (long i = 0; i < n; i++)
		{
			var bp = b[i];
			var ap = a[i];
			int max = Math.Max(
				Math.Max(Math.Abs(bp.R - ap.R), Math.Abs(bp.G - ap.G)),
				Math.Max(Math.Abs(bp.B - ap.B), Math.Abs(bp.A - ap.A)));

			diff[i] = max > channelTolerance
				? new Rgba32(255, 0, 0, 255) // changed → red
				// Dim the unchanged baseline so the red diff pops.
				: new Rgba32((byte)(bp.R / 3), (byte)(bp.G / 3), (byte)(bp.B / 3), 255);
		}

		using var diffImg = Image.LoadPixelData<Rgba32>(diff, w, h);
		using var ms = new MemoryStream();
		diffImg.SaveAsPng(ms);
		return ms.ToArray();
	}

	private static void WriteArtifact(string dir, string name, byte[] bytes)
	{
		try
		{
			Directory.CreateDirectory(dir);
			File.WriteAllBytes(Path.Combine(dir, name), bytes);
		}
		catch
		{
			// Best-effort diagnostics — never mask the real assertion failure
			// with an artifact-write IOException.
		}
	}
}
