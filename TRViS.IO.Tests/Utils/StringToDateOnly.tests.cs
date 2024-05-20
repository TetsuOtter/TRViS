namespace TRViS.IO.Tests;

public class StringToDateOnlyTests
{
	[Test]
	public void EmptyStringTest()
	{
		Assert.That(Utils.TryStringToDateOnly("", out DateOnly date), Is.False);
		Assert.That(date, Is.EqualTo(default(DateOnly)));
	}

	[Test]
	public void NullStringTest()
	{
		Assert.That(Utils.TryStringToDateOnly(null, out DateOnly date), Is.False);
		Assert.That(date, Is.EqualTo(default(DateOnly)));
	}

	[TestCase("20230318")]
	[TestCase("2023-03-18")]
	[TestCase("2023-3-18")]
	public void ValidStringTest(string input)
	{
		Assert.That(Utils.TryStringToDateOnly(input, out DateOnly date), Is.True);
		Assert.That(date, Is.EqualTo(new DateOnly(2023, 3, 18)));
	}
}
