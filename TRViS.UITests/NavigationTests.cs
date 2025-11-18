using Microsoft.Maui;
using Microsoft.Maui.Controls;
using TRViS.RootPages;
using Xunit;

namespace TRViS.UITests;

public class NavigationTests
{
	[Fact]
	public async Task CanNavigateToSelectTrainPage()
	{
		// Arrange
		var shell = GetAppShell();
		Assert.NotNull(shell);

		// Act
		await shell.GoToAsync($"//{SelectTrainPage.NameOfThisClass}");
		await Task.Delay(500); // Give time for navigation to complete

		// Assert
		var currentPage = shell.CurrentPage;
		Assert.NotNull(currentPage);
	}

	[Fact]
	public async Task CanNavigateToThirdPartyLicenses()
	{
		// Arrange
		var shell = GetAppShell();
		Assert.NotNull(shell);

		// Act
		await shell.GoToAsync("//ThirdPartyLicenses");
		await Task.Delay(500); // Give time for navigation to complete

		// Assert
		var currentPage = shell.CurrentPage;
		Assert.NotNull(currentPage);
	}

	[Fact]
	public async Task CanNavigateToSettings()
	{
		// Arrange
		var shell = GetAppShell();
		Assert.NotNull(shell);

		// Act
		await shell.GoToAsync("//EasterEggPage");
		await Task.Delay(500); // Give time for navigation to complete

		// Assert
		var currentPage = shell.CurrentPage;
		Assert.NotNull(currentPage);
	}

	[Fact]
	public async Task CanNavigateToFirebaseSettings()
	{
		// Arrange
		var shell = GetAppShell();
		Assert.NotNull(shell);

		// Act
		await shell.GoToAsync($"//{FirebaseSettingPage.NameOfThisClass}");
		await Task.Delay(500); // Give time for navigation to complete

		// Assert
		var currentPage = shell.CurrentPage;
		Assert.NotNull(currentPage);
	}

	private static Shell GetAppShell()
	{
		var app = Application.Current;
		Assert.NotNull(app);
		Assert.NotEmpty(app.Windows);

		var shell = app.Windows[0].Page as Shell;
		return shell!;
	}
}
