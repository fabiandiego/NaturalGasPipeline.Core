using NaturalGasPipeline.Core.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Core.Tests;

/// <summary>
/// Brent's method against roots known to full double precision.
/// </summary>
public class BrentRootSolverTests
{
    private readonly ITestOutputHelper _output;

    public BrentRootSolverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FindRoot_ShouldSolveWallisCubic()
    {
        // x³ − 2x − 5 = 0, Wallis' classic test; real root 2.0945514815423265…
        double root = BrentRootSolver.FindRoot(x => x * x * x - 2.0 * x - 5.0, 2.0, 3.0);

        _output.WriteLine($"x = {root:R}");

        Assert.Equal(2.0945514815423265, root, 1e-13);
    }

    [Fact]
    public void FindRoot_ShouldSolveTranscendentalEquation()
    {
        // cos x = x (Dottie number 0.7390851332151607…)
        double root = BrentRootSolver.FindRoot(x => System.Math.Cos(x) - x, 0.0, 1.0);

        _output.WriteLine($"x = {root:R}");

        Assert.Equal(0.7390851332151607, root, 1e-13);
    }

    [Fact]
    public void FindRoot_ShouldHandleSteepFunctionNearPole()
    {
        // Same shape as the DAK residual: → −∞ at the lower end of the bracket.
        double root = BrentRootSolver.FindRoot(x => 1.0 - 0.5 / x, 1e-10, 3.0);

        Assert.Equal(0.5, root, 1e-13);
    }

    [Fact]
    public void FindRoot_ShouldReturnExactEndpointRoot()
    {
        Assert.Equal(1.0, BrentRootSolver.FindRoot(x => x - 1.0, 1.0, 2.0));
    }

    [Fact]
    public void FindRoot_ShouldRejectBracketWithoutSignChange()
    {
        Assert.Throws<ArgumentException>(() => BrentRootSolver.FindRoot(x => x * x + 1.0, -1.0, 1.0));
        Assert.Throws<ArgumentException>(() => BrentRootSolver.FindRoot(x => x, 1.0, -1.0));
    }

    [Fact]
    public void FindRoot_ShouldRejectNonFiniteFunctionValues()
    {
        Assert.Throws<InvalidOperationException>(() => BrentRootSolver.FindRoot(x => double.NaN, 0.0, 1.0));
    }
}
