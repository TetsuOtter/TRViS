using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

using UIKit;

namespace TRViS;

// ref: https://github.com/dotnet/maui/issues/23380#issuecomment-2386743442
public sealed class HideShellTabRenderer : ShellRenderer
{
	private sealed class MyShellTabBarAppearanceTracker : ShellTabBarAppearanceTracker
	{
		public override void SetAppearance(UITabBarController controller, ShellAppearance appearance)
		{
			base.SetAppearance(controller, appearance);

			if (OperatingSystem.IsIOSVersionAtLeast(18))
			{
				controller.TabBarHidden = true;
			}
		}
	}
	protected override IShellTabBarAppearanceTracker CreateTabBarAppearanceTracker() => new MyShellTabBarAppearanceTracker();

	// Use custom ShellItemRenderer to fix iOS 12.x crashes (NullReferenceException in TraitCollectionDidChange)
	protected override IShellItemRenderer CreateShellItemRenderer(ShellItem shellItem)
	{
		var renderer = new iOS12CompatShellItemRenderer(this);
		renderer.ShellItem = shellItem;
		return renderer;
	}
}
