using NaturalGasPipeline.Core.Math;

namespace NaturalGasPipeline.Tests;

public class CubicEquationSolverTests
{
    [Fact]
    public void ShouldFindKnownRoot()
    {
        // x³ - 6x² + 11x - 6 = 0
        //
        // Roots:
        // 1, 2, 3

        double root =
            CubicEquationSolver.LargestRealRoot(
                1,
                -6,
                11,
                -6);

        Assert.Equal(3.0, root, 10);
    }
}