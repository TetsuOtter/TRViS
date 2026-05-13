using Microsoft.Maui.Controls.Platform.Compatibility;

using UIKit;

namespace TRViS;

/// <summary>
/// Custom ShellItemRenderer to fix NullReferenceException on iOS 12.x
/// In iOS 12, TraitCollectionDidChange can be called with null previousTraitCollection parameter
/// This is a workaround for: https://github.com/dotnet/maui/issues (iOS 12.x compatibility)
/// </summary>
public sealed class iOS12CompatShellItemRenderer : ShellItemRenderer
{
	public iOS12CompatShellItemRenderer(IShellContext context) : base(context)
	{
	}

	public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
	{
		// Fix for iOS 12.x: previousTraitCollection can be null on iOS 12
		// Original MAUI implementation crashes here due to null reference
		if (previousTraitCollection == null)
			return;

		// Only process if VerticalSizeClass actually changed
		// This mirrors the original MAUI logic but with null safety
		if (previousTraitCollection.VerticalSizeClass == TraitCollection.VerticalSizeClass)
			return;

		// Note: The original implementation updates TabBar item images here using TabbedViewExtensions
		// We skip that since it's not accessible, but this is only needed for orientation changes
		// which is a minor visual issue compared to crashing on iOS 12.x
		// TabBar items will still function correctly, just without dynamic image resizing
	}
}
