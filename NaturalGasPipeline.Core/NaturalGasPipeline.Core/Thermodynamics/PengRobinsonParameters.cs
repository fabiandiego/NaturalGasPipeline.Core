using UnitsNet;

namespace NaturalGasPipeline.Core.Thermodynamics;

public class PengRobinsonParameters
{
    private const double R = 8.31446261815324;

    public double A { get; }
    public double B { get; }
    public double Alpha { get; }
    public double Kappa { get; }

    public PengRobinsonParameters(
        GasComponent component,
        Temperature temperature)
    {
        double tc = component.CriticalTemperature.Kelvins;
        double pc = component.CriticalPressure.Pascals;
        double omega = component.AcentricFactor;
        double t = temperature.Kelvins;

        Kappa =
            0.37464
            + 1.54226 * omega
            - 0.26992 * omega * omega;

        Alpha =
            System.Math.Pow(
                1 + Kappa *
                (1 - System.Math.Sqrt(t / tc)),
                2);

        A =
            0.45724
            * R * R
            * tc * tc
            / pc
            * Alpha;

        B =
            0.07780
            * R
            * tc
            / pc;
    }

    public PengRobinsonParameters(
        GasMixture mixture,
        Temperature temperature,
        BinaryInteractionDatabase? interactionDatabase = null)
    {
        mixture.ValidateComposition();

        var parameters = new List<PengRobinsonParameters>();

        foreach (var component in mixture.Components)
        {
            parameters.Add(
                new PengRobinsonParameters(
                    component,
                    temperature));
        }

        double aMix = 0.0;
        double bMix = 0.0;

        for (int i = 0; i < mixture.Components.Count; i++)
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
                        mixture.Components[j].Name) ?? 0.0;

                double aij =
                    CalculateInteractionA(
                        parameters[i].A,
                        parameters[j].A,
                        kij);

                aMix +=
                    yi * yj * aij;
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