using SystemMath = System.Math;

namespace NaturalGasPipeline.Core.Math;

public static class CubicEquationSolver
{
    private const double Tolerance = 1e-12;

    public static double LargestRealRoot(
        double a,
        double b,
        double c,
        double d)
    {
        if (SystemMath.Abs(a) < Tolerance)
            throw new ArgumentException(
                "Coefficient 'a' cannot be zero.");

        double A = b / a;
        double B = c / a;
        double C = d / a;

        double p = B - A * A / 3.0;

        double q =
            2.0 * A * A * A / 27.0
            - A * B / 3.0
            + C;

        double discriminant =
            q * q / 4.0
            + p * p * p / 27.0;

        // One real root
        if (discriminant > Tolerance)
        {
            double sqrtDiscriminant =
                SystemMath.Sqrt(discriminant);

            double u =
                CubeRoot(-q / 2.0 + sqrtDiscriminant);

            double v =
                CubeRoot(-q / 2.0 - sqrtDiscriminant);

            double singleRoot =
                u + v - A / 3.0;

            return singleRoot;
        }

        // Multiple real roots
        if (SystemMath.Abs(discriminant) <= Tolerance)
        {
            double u =
                CubeRoot(-q / 2.0);

            double multipleRootA =
                2.0 * u - A / 3.0;

            double multipleRootB =
                -u - A / 3.0;

            return SystemMath.Max(
                multipleRootA,
                multipleRootB);
        }

        // Three distinct real roots

        double r =
            2.0 * SystemMath.Sqrt(-p / 3.0);

        double acosArgument =
            (-q / 2.0) /
            SystemMath.Sqrt(-(p * p * p) / 27.0);

        acosArgument =
            SystemMath.Clamp(
                acosArgument,
                -1.0,
                1.0);

        double theta =
            SystemMath.Acos(acosArgument);

        double realRootA =
            r * SystemMath.Cos(theta / 3.0)
            - A / 3.0;

        double realRootB =
            r * SystemMath.Cos(
                (theta + 2.0 * SystemMath.PI) / 3.0)
            - A / 3.0;

        double realRootC =
            r * SystemMath.Cos(
                (theta + 4.0 * SystemMath.PI) / 3.0)
            - A / 3.0;

        return SystemMath.Max(
            realRootA,
            SystemMath.Max(
                realRootB,
                realRootC));
    }

    private static double CubeRoot(double x)
    {
        return SystemMath.Sign(x) *
               SystemMath.Pow(
                   SystemMath.Abs(x),
                   1.0 / 3.0);
    }
}