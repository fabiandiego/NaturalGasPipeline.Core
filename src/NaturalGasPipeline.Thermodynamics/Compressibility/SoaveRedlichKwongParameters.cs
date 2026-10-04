using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsNet;
using NaturalGasPipeline.Core.Common;

namespace NaturalGasPipeline.Thermodynamics;

public class SoaveRedlichKwongParameters
{
    private const double R = PhysicalConstants.MolarGasConstant;

    public double A { get; }
    public double B { get; }
    public double Alpha { get; }
    public double Kappa { get; }

    public SoaveRedlichKwongParameters(
        GasComponent component,
        Temperature temperature)
    {
        double tc =
            component.CriticalTemperature.Kelvins;

        double pc =
            component.CriticalPressure.Pascals;

        double omega =
            component.AcentricFactor;

        double t =
            temperature.Kelvins;

        Kappa =
            0.480
            + 1.574 * omega
            - 0.176 * omega * omega;

        Alpha =
            System.Math.Pow(
                1.0
                + Kappa *
                (1.0 - System.Math.Sqrt(t / tc)),
                2);

        A =
            0.42747
            * R * R
            * tc * tc
            / pc
            * Alpha;

        B =
            0.08664
            * R
            * tc
            / pc;
    }

    public SoaveRedlichKwongParameters(
        GasMixture mixture,
        Temperature temperature,
        BinaryInteractionDatabase? interactionDatabase = null)
    {
        mixture.ValidateComposition();

        var parameters =
            new List<SoaveRedlichKwongParameters>();

        foreach (var component in mixture.Components)
        {
            parameters.Add(
                new SoaveRedlichKwongParameters(
                    component,
                    temperature));
        }

        double aMix = 0.0;
        double bMix = 0.0;

        for (int i = 0;
             i < mixture.Components.Count;
             i++)
        {
            double yi =
                mixture.Components[i].MoleFraction;

            bMix +=
                yi * parameters[i].B;

            for (int j = 0;
                 j < mixture.Components.Count;
                 j++)
            {
                double yj =
                    mixture.Components[j].MoleFraction;

                double kij =
                    interactionDatabase?.Get(
                        mixture.Components[i].Name,
                        mixture.Components[j].Name)
                    ?? 0.0;

                double aij =
                    CalculateInteractionA(
                        parameters[i].A,
                        parameters[j].A,
                        kij);

                aMix +=
                    yi
                    * yj
                    * aij;
            }
        }

        A = aMix;
        B = bMix;

        Alpha = 0.0;
        Kappa = 0.0;
    }

    private static double CalculateInteractionA(
        double ai,
        double aj,
        double kij)
    {
        return System.Math.Sqrt(ai * aj)
               * (1.0 - kij);
    }
}