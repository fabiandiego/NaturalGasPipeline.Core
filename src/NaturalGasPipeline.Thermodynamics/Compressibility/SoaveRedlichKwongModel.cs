using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsNet;
using NaturalGasPipeline.Core.Numerics;
using NaturalGasPipeline.Core.Common;

namespace NaturalGasPipeline.Thermodynamics;

public class SoaveRedlichKwongModel : ICompressibilityModel
{
    private const double R = PhysicalConstants.MolarGasConstant;

    private readonly GasComponent? _component;
    private readonly GasMixture? _mixture;
    private readonly BinaryInteractionDatabase? _interactionDatabase;

    public SoaveRedlichKwongModel(
        GasComponent component)
    {
        _component = component;
    }

    public SoaveRedlichKwongModel(
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
        SoaveRedlichKwongParameters parameters;

        if (_mixture != null)
        {
            parameters =
                new SoaveRedlichKwongParameters(
                    _mixture,
                    temperature,
                    _interactionDatabase);
        }
        else if (_component != null)
        {
            parameters =
                new SoaveRedlichKwongParameters(
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

        // SRK cubic equation:
        //
        // Z³ - Z²
        // + (A - B - B²)Z
        // - AB = 0

        double c2 = -1.0;

        double c1 =
            A
            - B
            - B * B;

        double c0 =
            -A * B;

        return CubicEquationSolver.LargestRealRoot(
            1.0,
            c2,
            c1,
            c0);
    }
}