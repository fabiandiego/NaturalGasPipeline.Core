namespace NaturalGasPipeline.Core.Numerics;

/// <summary>
/// Brent's method (Brent 1973, "Algorithms for Minimization without
/// Derivatives", ch. 4): bisection safeguarded by secant and inverse
/// quadratic interpolation. Always converges for a continuous function
/// whose values at the bracket ends have opposite signs, and is
/// superlinear near a simple root.
/// </summary>
public static class BrentRootSolver
{
    // IEEE 754 double machine epsilon (2⁻⁵²). Not System.Double.Epsilon,
    // which is the smallest subnormal number.
    private const double MachineEpsilon = 2.220446049250313e-16;

    /// <summary>
    /// Root of <paramref name="function"/> in [lower, upper].
    /// </summary>
    /// <param name="function">Continuous function.</param>
    /// <param name="lower">Lower end of the bracket.</param>
    /// <param name="upper">Upper end of the bracket.</param>
    /// <param name="absoluteTolerance">Tolerance on x.</param>
    /// <param name="maxIterations">Iteration limit; exceeding it throws.</param>
    /// <exception cref="ArgumentException">The bracket does not contain a sign change.</exception>
    /// <exception cref="InvalidOperationException">No convergence, or a non-finite function value.</exception>
    public static double FindRoot(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance = 1e-14,
        int maxIterations = 200)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (!double.IsFinite(lower) || !double.IsFinite(upper) || lower >= upper)
        {
            throw new ArgumentException("The bracket must be finite with lower < upper.");
        }

        if (!double.IsFinite(absoluteTolerance) || absoluteTolerance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteTolerance), absoluteTolerance, "Tolerance must be finite and greater than zero.");
        }

        double a = lower;
        double b = upper;
        double fa = Evaluate(function, a);
        double fb = Evaluate(function, b);

        if (fa == 0.0)
        {
            return a;
        }

        if (fb == 0.0)
        {
            return b;
        }

        if (fa * fb > 0.0)
        {
            throw new ArgumentException(
                $"The function has the same sign at both ends of the bracket " +
                $"(f({lower}) = {fa}, f({upper}) = {fb}).");
        }

        double c = b;
        double fc = fb;
        double d = b - a;
        double e = d;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            // Keep the root between b and c.
            if (fb * fc > 0.0)
            {
                c = a;
                fc = fa;
                d = b - a;
                e = d;
            }

            // b is the best estimate: |f(b)| ≤ |f(c)|.
            if (System.Math.Abs(fc) < System.Math.Abs(fb))
            {
                a = b;
                b = c;
                c = a;
                fa = fb;
                fb = fc;
                fc = fa;
            }

            double tol = 2.0 * MachineEpsilon * System.Math.Abs(b) + 0.5 * absoluteTolerance;
            double half = 0.5 * (c - b);

            if (System.Math.Abs(half) <= tol || fb == 0.0)
            {
                return b;
            }

            if (System.Math.Abs(e) >= tol && System.Math.Abs(fa) > System.Math.Abs(fb))
            {
                double p;
                double q;
                double s = fb / fa;

                if (a == c)
                {
                    // Secant step.
                    p = 2.0 * half * s;
                    q = 1.0 - s;
                }
                else
                {
                    // Inverse quadratic interpolation.
                    double qa = fa / fc;
                    double r = fb / fc;
                    p = s * (2.0 * half * qa * (qa - r) - (b - a) * (r - 1.0));
                    q = (qa - 1.0) * (r - 1.0) * (s - 1.0);
                }

                if (p > 0.0)
                {
                    q = -q;
                }
                else
                {
                    p = -p;
                }

                double limit = System.Math.Min(
                    3.0 * half * q - System.Math.Abs(tol * q),
                    System.Math.Abs(e * q));

                if (2.0 * p < limit)
                {
                    e = d;
                    d = p / q;
                }
                else
                {
                    d = half;
                    e = d;
                }
            }
            else
            {
                d = half;
                e = d;
            }

            a = b;
            fa = fb;

            b += System.Math.Abs(d) > tol
                ? d
                : (half > 0.0 ? tol : -tol);

            fb = Evaluate(function, b);
        }

        throw new InvalidOperationException(
            $"Brent's method did not converge in {maxIterations} iterations.");
    }

    private static double Evaluate(Func<double, double> function, double x)
    {
        double value = function(x);

        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException(
                $"The function returned a non-finite value ({value}) at x = {x}.");
        }

        return value;
    }
}
