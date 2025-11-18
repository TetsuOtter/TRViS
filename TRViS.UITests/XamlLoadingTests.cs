using System.Xml.Linq;
using System.Reflection;

namespace TRViS.UITests;

[TestFixture]
public class XamlLoadingTests
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
	public void AppShellXamlIsValid()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange
		var resourceName = _appAssembly.GetManifestResourceNames()
			.FirstOrDefault(n => n.Contains("AppShell.xaml") && !n.Contains(".g."));

		// Assert  
		Assert.That(resourceName, Is.Not.Null, "AppShell.xaml should be embedded as a resource");

		if (resourceName != null)
		{
			using var stream = _appAssembly.GetManifestResourceStream(resourceName);
			Assert.That(stream, Is.Not.Null);

			// Try to parse as XML
			var xaml = XDocument.Load(stream!);
			Assert.That(xaml, Is.Not.Null);
			Assert.That(xaml.Root, Is.Not.Null);
		}
	}

	[Test]
	public void AppXamlIsValid()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange
		var resourceName = _appAssembly.GetManifestResourceNames()
			.FirstOrDefault(n => n.Contains("App.xaml") && !n.Contains("Shell") && !n.Contains(".g."));

		// Assert
		Assert.That(resourceName, Is.Not.Null, "App.xaml should be embedded as a resource");

		if (resourceName != null)
		{
			using var stream = _appAssembly.GetManifestResourceStream(resourceName);
			Assert.That(stream, Is.Not.Null);

			// Try to parse as XML
			var xaml = XDocument.Load(stream!);
			Assert.That(xaml, Is.Not.Null);
			Assert.That(xaml.Root, Is.Not.Null);
		}
	}

	[Test]
	public void AllXamlResourcesCanBeParsed()
	{
		if (_appAssembly == null)
		{
			Assert.Ignore("TRViS assembly not available. Set TRVIS_ASSEMBLY_PATH environment variable.");
			return;
		}

		// Arrange
		var xamlResources = _appAssembly.GetManifestResourceNames()
			.Where(n => n.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) && !n.Contains(".g."))
			.ToList();

		// Assert
		Assert.That(xamlResources, Is.Not.Empty, "Should have at least one XAML resource");

		foreach (var resourceName in xamlResources)
		{
			using var stream = _appAssembly.GetManifestResourceStream(resourceName);
			Assert.That(stream, Is.Not.Null, $"Resource stream for {resourceName} should not be null");

			// Try to parse as XML
			var xaml = XDocument.Load(stream!);
			Assert.That(xaml, Is.Not.Null, $"{resourceName} should be valid XML");
			Assert.That(xaml.Root, Is.Not.Null, $"{resourceName} should have a root element");
		}
	}
}
