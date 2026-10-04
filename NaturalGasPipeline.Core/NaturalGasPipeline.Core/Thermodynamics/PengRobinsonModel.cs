using UnitsNet;
using NaturalGasPipeline.Core.Math;

namespace NaturalGasPipeline.Core.Thermodynamics;

public class PengRobinsonModel : ICompressibilityModel
{
    private const double R = 8.31446261815324;

    private readonly GasComponent? _component;
    private readonly GasMixture? _mixture;
    private readonly BinaryInteractionDatabase? _interactionDatabase;

    public PengRobinsonModel(GasComponent component)
    {
        _component = component;
    }

    public PengRobinsonModel(
        GasMixture mixture,
        BinaryInteractionDatabase? interactionDatabase = null)
    {
        _mixture = mixture;
        _interactionDatabase = interactionDatabase;
    }

    public double Calculate(
        Pressure pressure,
        Temperature temperature)
    {
        PengRobinsonParameters parameters;

        if (_mixture != null)
        {
            parameters =
                new PengRobinsonParameters(
                    _mixture,
                    temperature,
                    _interactionDatabase);
        }
        else if (_component != null)
        {
            parameters =
                new PengRobinsonParameters(
                    _component,
                    temperature);
        }
        else
        {
            throw new InvalidOperationException(
                "No gas component or mixture was provided.");
        }

        double a = parameters.A;
        double b = parameters.B;

        double p = pressure.Pascals;
        double t = temperature.Kelvins;

        double A =
            a * p /
            (R * R * t * t);

        double B =
            b * p /
            (R * t);

        double c2 = -(1 - B);

        double c1 =
            A
            - 3 * B * B
            - 2 * B;

        double c0 =
            -(A * B
            - B * B
            - B * B * B);

        return CubicEquationSolver.LargestRealRoot(
            1,
            c2,
            c1,
            c0);
    }
}