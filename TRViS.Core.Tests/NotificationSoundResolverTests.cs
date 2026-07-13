namespace TRViS.Core.Tests;

public class NotificationSoundResolverTests
{
	private static readonly SoundRef Individual = new("aW5kaXZpZHVhbA==", "wav");
	private static readonly SoundRef Default = new("ZGVmYXVsdA==", "mp3");

	[Fact]
	public void Resolve_IndividualSpecified_ReturnsIndividual()
	{
		var result = NotificationSoundResolver.Resolve(Individual, Default);

		Assert.Same(Individual, result);
	}

	[Fact]
	public void Resolve_NoIndividual_FallsBackToDefault()
	{
		var result = NotificationSoundResolver.Resolve(null, Default);

		Assert.Same(Default, result);
	}

	[Fact]
	public void Resolve_NeitherSpecified_ReturnsNull()
	{
		var result = NotificationSoundResolver.Resolve(null, null);

		Assert.Null(result);
	}

	[Fact]
	public void Resolve_IndividualAndNoDefault_ReturnsIndividual()
	{
		var result = NotificationSoundResolver.Resolve(Individual, null);

		Assert.Same(Individual, result);
	}
}
