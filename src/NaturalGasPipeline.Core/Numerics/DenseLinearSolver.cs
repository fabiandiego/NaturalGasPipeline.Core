using NaturalGasPipeline.Core.Common;
namespace NaturalGasPipeline.Core.Numerics;

/// <summary>
/// Gaussian elimination with partial pivoting for small dense systems
/// A·x = b (network Jacobians of up to a few hundred nodes).
/// </summary>
public static class DenseLinearSolver
{
    /// <summary>Solves A·x = b. A and b are not modified.</summary>
    /// <exception cref="InvalidOperationException">A is singular to working precision.</exception>
    public static double[] Solve(double[,] a, double[] b)
    {
        Guard.NotNull(a);
        Guard.NotNull(b);

        int n = b.Length;

        if (a.GetLength(0) != n || a.GetLength(1) != n)
        {
            throw new ArgumentException("A must be square and match the length of b.");
        }

        var m = (double[,])a.Clone();
        var x = (double[])b.Clone();

        double scale = 0.0;
        foreach (double v in a)
        {
            scale = System.Math.Max(scale, System.Math.Abs(v));
        }

        for (int k = 0; k < n; k++)
        {
            int pivot = k;
            for (int i = k + 1; i < n; i++)
            {
                if (System.Math.Abs(m[i, k]) > System.Math.Abs(m[pivot, k]))
                {
                    pivot = i;
                }
            }

            if (!(System.Math.Abs(m[pivot, k]) > 1e-14 * scale))
            {
                throw new InvalidOperationException($"Matrix is singular (pivot {k}).");
            }

            if (pivot != k)
            {
                for (int j = 0; j < n; j++)
                {
                    (m[k, j], m[pivot, j]) = (m[pivot, j], m[k, j]);
                }

                (x[k], x[pivot]) = (x[pivot], x[k]);
            }

            for (int i = k + 1; i < n; i++)
            {
                double factor = m[i, k] / m[k, k];

                if (factor == 0.0)
                {
                    continue;
                }

                for (int j = k; j < n; j++)
                {
                    m[i, j] -= factor * m[k, j];
                }

                x[i] -= factor * x[k];
            }
        }

        for (int i = n - 1; i >= 0; i--)
        {
            double sum = x[i];
            for (int j = i + 1; j < n; j++)
            {
                sum -= m[i, j] * x[j];
            }

            x[i] = sum / m[i, i];
        }

        return x;
    }
}
