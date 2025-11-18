using System.Reflection;

namespace TRViS.UITests;

[TestFixture]
public class AppStructureTests
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
	public void AppClassExists()
	{
		// Skip if assembly is not available
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange & Act
		var appType = _appAssembly.GetType("TRViS.App");

		// Assert
		Assert.That(appType, Is.Not.Null, "App class should exist");
	}

	[Test]
	public void AppShellClassExists()
	{
		// Skip if assembly is not available
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange & Act
		var shellType = _appAssembly.GetType("TRViS.AppShell");

		// Assert
		Assert.That(shellType, Is.Not.Null, "AppShell class should exist");
	}

	[Test]
	public void MauiProgramClassExists()
	{
		// Skip if assembly is not available
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange & Act
		var mauiProgramType = _appAssembly.GetType("TRViS.MauiProgram");

		// Assert
		Assert.That(mauiProgramType, Is.Not.Null, "MauiProgram class should exist");
	}

	[Test]
	public void MauiProgramHasCreateMauiAppMethod()
	{
		// Skip if assembly is not available
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange
		var mauiProgramType = _appAssembly.GetType("TRViS.MauiProgram");
		Assert.That(mauiProgramType, Is.Not.Null);

		// Act
		var method = mauiProgramType!.GetMethod("CreateMauiApp", BindingFlags.Public | BindingFlags.Static);

		// Assert
		Assert.That(method, Is.Not.Null, "CreateMauiApp method should exist");
		Assert.That(method!.ReturnType.Name, Is.EqualTo("MauiApp"), "Method should return MauiApp");
	}
}
