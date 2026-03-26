// Disable parallel test fixture execution: each test creates its own Appium session
// for the same app process, so concurrent sessions would interfere with each other.
[assembly: NUnit.Framework.Parallelizable(NUnit.Framework.ParallelScope.None)]
