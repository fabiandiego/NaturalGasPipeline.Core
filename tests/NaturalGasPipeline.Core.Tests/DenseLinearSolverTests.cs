using NaturalGasPipeline.Core.Numerics;
using Xunit;

namespace NaturalGasPipeline.Core.Tests;

public class DenseLinearSolverTests
{
    [Fact]
    public void Solve_ShouldReturnTheExactSolutionOfAKnownSystem()
    {
        // x = (1, −2, 3); the first pivot is zero, so pivoting is required.
        var a = new double[,]
        {
            { 0.0, 2.0, 1.0 },
            { 1.0, 1.0, 1.0 },
            { 2.0, 1.0, -1.0 },
        };
        var b = new[] { -1.0, 2.0, -3.0 };

        double[] x = DenseLinearSolver.Solve(a, b);

        Assert.Equal(1.0, x[0], 12);
        Assert.Equal(-2.0, x[1], 12);
        Assert.Equal(3.0, x[2], 12);
        Assert.Equal(0.0, a[0, 0]); // input untouched
    }

    [Fact]
    public void Solve_ShouldHandleBadlyScaledRows()
    {
        // Rows in Pa-scale and kg/s-scale, as in a network Jacobian.
        var a = new double[,] { { 1e-5, -4e-6 }, { -4e-6, 9e-6 } };
        var b = new[] { 1e-5 * 2e6 - 4e-6 * 1e6, -4e-6 * 2e6 + 9e-6 * 1e6 };

        double[] x = DenseLinearSolver.Solve(a, b);

        Assert.Equal(2e6, x[0], 1e-4);
        Assert.Equal(1e6, x[1], 1e-4);
    }

    [Fact]
    public void Solve_ShouldRejectSingularMatrices()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DenseLinearSolver.Solve(new double[,] { { 1.0, 2.0 }, { 2.0, 4.0 } }, new[] { 1.0, 2.0 }));
        Assert.Throws<ArgumentException>(() =>
            DenseLinearSolver.Solve(new double[,] { { 1.0, 2.0 } }, new[] { 1.0, 2.0 }));
    }
}
