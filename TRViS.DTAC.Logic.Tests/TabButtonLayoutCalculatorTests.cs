using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Tests;

public class TabButtonLayoutCalculatorTests
{
	[Fact]
	public void ZeroWindowWidth_ReturnsZero()
	{
		double result = TabButtonLayoutCalculator.CalculateWidthRequest(0, 3, 152);
		Assert.Equal(0, result);
	}

	[Fact]
	public void NegativeWindowWidth_ReturnsZero()
	{
		double result = TabButtonLayoutCalculator.CalculateWidthRequest(-100, 3, 152);
		Assert.Equal(0, result);
	}

	[Fact]
	public void ZeroTabButtonCount_ReturnsZero()
	{
		double result = TabButtonLayoutCalculator.CalculateWidthRequest(400, 0, 152);
		Assert.Equal(0, result);
	}

	[Fact]
	public void NarrowWindow_WidthClampedBelowMax()
	{
		// (100 - 8) / 3 = 30.67, which is well below 152
		double result = TabButtonLayoutCalculator.CalculateWidthRequest(100, 3, 152);
		Assert.Equal((100.0 - 8) / 3, result, precision: 5);
	}

	[Fact]
	public void WideWindow_WidthClampedToMax()
	{
		// (1000 - 8) / 3 = 330.67, which exceeds 152
		double result = TabButtonLayoutCalculator.CalculateWidthRequest(1000, 3, 152);
		Assert.Equal(152, result);
	}

	[Fact]
	public void ExactlyAtMax_ReturnsMax()
	{
		// Make (w - 8) / 3 exactly equal to maxWidth=152
		// w = 152 * 3 + 8 = 464
		double result = TabButtonLayoutCalculator.CalculateWidthRequest(464, 3, 152);
		Assert.Equal(152, result);
	}

	[Fact]
	public void OnePixelBelowMax_ReturnsCalculated()
	{
		// (463 - 8) / 3 = 151.667
		double result = TabButtonLayoutCalculator.CalculateWidthRequest(463, 3, 152);
		Assert.Equal((463.0 - 8) / 3, result, precision: 5);
	}

	[Fact]
	public void TwoTabButtons_DividesByTwo()
	{
		// (208 - 8) / 2 = 100, below maxWidth=152
		double result = TabButtonLayoutCalculator.CalculateWidthRequest(208, 2, 152);
		Assert.Equal(100, result);
	}
}
