using NaturalGasPipeline.Core.Numerics;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Core.Tests;

public class CubicEquationSolverTests
{
    private readonly ITestOutputHelper _output;

    public CubicEquationSolverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ShouldFindKnownRoot()
    {
        // x³ - 6x² + 11x - 6 = 0  → roots 1, 2, 3
        double root =
            CubicEquationSolver.LargestRealRoot(1, -6, 11, -6);

        Assert.Equal(3.0, root, 10);
    }

    [Fact]
    public void DoubleRoot_ShouldReturnLargestRoot()
    {
        // (x - 1)²(x - 2) = x³ - 4x² + 5x - 2  → roots 1, 1, 2
        // Exercises the zero-discriminant branch.
        double root =
            CubicEquationSolver.LargestRealRoot(1, -4, 5, -2);

        _output.WriteLine($"Largest root: {root:R}");

        Assert.Equal(2.0, root, 8);
    }

    [Fact]
    public void PengRobinsonTwoPhaseRegion_ShouldReturnVaporRoot()
    {
        // PR cubic for methane at 140 K and 0.5 MPa (below the saturation
        // pressure, so three real roots exist: liquid, unstable, vapor).
        // Roots from numpy.roots + Newton polishing:
        //   0.016601827159069736 (liquid)
        //   0.07002373629540896  (unstable)
        //   0.9018613995499012   (vapor)  ← the gas-phase Z
        const double expectedVapor = 0.9018613995499012;

        double root =
            CubicEquationSolver.LargestRealRoot(
                1.0,
                -0.9884869630043799,
                0.0792867738608636,
                -0.001048433688173932);

        _output.WriteLine($"Largest root: {root:R}");
        _output.WriteLine($"Reference:    {expectedVapor:R}");

        Assert.Equal(expectedVapor, root, 10);
    }
}
