using System.Reflection;

namespace TRViS.UITests;

[TestFixture]
public class PageStructureTests
{
	private Assembly? _appAssembly;

	[SetUp]
	public void Setup()
	{
		// Try to load the TRViS assembly from the build output
		var assemblyPath = Environment.GetEnvironmentVariable("TRVIS_ASSEMBLY_PATH");
		if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
		{
			_appAssembly = Assembly.LoadFrom(assemblyPath);
		}
	}

	[Test]
	public void SelectTrainPageExists()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange & Act
		var pageType = _appAssembly.GetType("TRViS.RootPages.SelectTrainPage");

		// Assert
		Assert.That(pageType, Is.Not.Null, "SelectTrainPage should exist");
	}

	[Test]
	public void ThirdPartyLicensesPageExists()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange & Act
		var pageType = _appAssembly.GetType("TRViS.RootPages.ThirdPartyLicenses");

		// Assert
		Assert.That(pageType, Is.Not.Null, "ThirdPartyLicenses page should exist");
	}

	[Test]
	public void EasterEggPageExists()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange & Act
		var pageType = _appAssembly.GetType("TRViS.RootPages.EasterEggPage");

		// Assert
		Assert.That(pageType, Is.Not.Null, "EasterEggPage should exist");
	}

	[Test]
	public void FirebaseSettingPageExists()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange & Act
		var pageType = _appAssembly.GetType("TRViS.RootPages.FirebaseSettingPage");

		// Assert
		Assert.That(pageType, Is.Not.Null, "FirebaseSettingPage should exist");
	}

	[Test]
	public void ShowMarkdownPageExists()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange & Act
		var pageType = _appAssembly.GetType("TRViS.RootPages.ShowMarkdownPage");

		// Assert
		Assert.That(pageType, Is.Not.Null, "ShowMarkdownPage should exist");
	}

	[Test]
	public void AllPagesHaveConstructors()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange
		var pageTypeNames = new[]
		{
			"TRViS.RootPages.SelectTrainPage",
			"TRViS.RootPages.ThirdPartyLicenses",
			"TRViS.RootPages.EasterEggPage",
			"TRViS.RootPages.FirebaseSettingPage",
			"TRViS.RootPages.ShowMarkdownPage",
		};

		// Act & Assert
		foreach (var pageTypeName in pageTypeNames)
		{
			var pageType = _appAssembly.GetType(pageTypeName);
			Assert.That(pageType, Is.Not.Null, $"{pageTypeName} should exist");
			
			if (pageType != null)
			{
				var constructor = pageType.GetConstructor(Type.EmptyTypes);
				Assert.That(constructor, Is.Not.Null, $"{pageTypeName} should have a parameterless constructor");
			}
		}
	}
}
