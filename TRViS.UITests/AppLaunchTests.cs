using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Xunit;

namespace TRViS.UITests;

public class AppLaunchTests
{
	[Fact]
	public void AppLaunches()
	{
		// Arrange & Act
		var app = Application.Current;

		// Assert
		Assert.NotNull(app);
		Assert.IsType<TRViS.App>(app);
	}

	[Fact]
	public void MainWindowCreated()
	{
		// Arrange
		var app = Application.Current;
		Assert.NotNull(app);

		// Act & Assert
		Assert.NotNull(app.Windows);
		Assert.NotEmpty(app.Windows);
	}

	[Fact]
	public void AppShellLoaded()
	{
		// Arrange
		var app = Application.Current;
		Assert.NotNull(app);
		Assert.NotEmpty(app.Windows);

		// Act
		var mainPage = app.Windows[0].Page;

		// Assert
		Assert.NotNull(mainPage);
		Assert.IsType<TRViS.AppShell>(mainPage);
	}
}
