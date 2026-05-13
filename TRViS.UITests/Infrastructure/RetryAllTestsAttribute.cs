using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal.Commands;

namespace TRViS.UITests.Infrastructure;

/// <summary>
/// Class-level wrapper around <see cref="RetryAttribute"/>. NUnit's built-in
/// <see cref="RetryAttribute"/> is restricted to <see cref="AttributeTargets.Method"/>
/// (despite some doc claims), which is awkward for fixtures whose every test
/// shares a single Appium / UIA2 driver instance — when the instrumentation
/// process crashes, every test in the fixture inherits the failure mode and
/// each one needs the same retry policy.
/// <para>
/// This attribute targets a TestFixture class; NUnit's test command pipeline
/// invokes <see cref="Wrap(TestCommand)"/> for every test method discovered
/// inside the fixture, applying <see cref="RetryAttribute.RetryCommand"/>
/// uniformly. Per-test SetUp/TearDown still re-runs between retries (NUnit
/// guarantees this), so a fresh Driver is constructed each attempt — exactly
/// what's required to recover from a dead UIA2 / XCUITest server.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RetryAllTestsAttribute : NUnitAttribute, IRepeatTest
{
	private readonly int _tryCount;

	public RetryAllTestsAttribute(int tryCount)
	{
		_tryCount = tryCount;
	}

	public TestCommand Wrap(TestCommand command) => new RetryAttribute.RetryCommand(command, _tryCount);
}
