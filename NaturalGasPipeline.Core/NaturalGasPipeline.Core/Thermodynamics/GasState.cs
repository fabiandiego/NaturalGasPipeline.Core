using UnitsNet;

namespace NaturalGasPipeline.Core.Thermodynamics;

public class GasState
{
    public Pressure Pressure { get; set; }

    public Temperature Temperature { get; set; }

    public double CompressibilityFactor { get; private set; }

    public double MolarMass { get; private set; }

    public Temperature CriticalTemperature { get; private set; }

    public Pressure CriticalPressure { get; private set; }

    public double ReducedTemperature { get; private set; }

    public double ReducedPressure { get; private set; }

    public Density Density { get; private set; }

    public GasMixture? Mixture { get; set; }

    public void CalculateCompressibility(
        ICompressibilityModel model)
    {
        CompressibilityFactor =
            model.Calculate(
                Pressure,
                Temperature);
    }

    public void CalculateMolarMass()
    {
        if (Mixture == null)
        {
            throw new InvalidOperationException(
                "Gas mixture has not been assigned.");
        }

        Mixture.ValidateComposition();

        MolarMass =
            Mixture.CalculateMolarMass();
    }

    public void CalculateDensity()
    {
        if (Mixture != null)
        {
            CalculateMolarMass();
        }

        const double R = 8.31446261815324;

        double p = Pressure.Pascals;
        double t = Temperature.Kelvins;
        double z = CompressibilityFactor;

        double density =
            p * MolarMass /
            (z * R * t);

        Density =
            UnitsNet.Density.FromKilogramsPerCubicMeter(
                density);
    }

    public void CalculateState(
    ICompressibilityModel model)
    {
        CalculateMolarMass();

        CriticalTemperature =
            Mixture.CalculateCriticalTemperature();

        CriticalPressure =
            Mixture.CalculateCriticalPressure();

        ReducedTemperature =
            Temperature.Kelvins /
            CriticalTemperature.Kelvins;

        ReducedPressure =
            Pressure.Pascals /
            CriticalPressure.Pascals;

        CalculateCompressibility(model);

        CalculateDensity();
    }
    public void CalculateState(
    CompressibilityModelType modelType,
    BinaryInteractionDatabase? interactionDatabase = null)
    {
        if (Mixture == null)
        {
            throw new InvalidOperationException(
                "Gas mixture has not been assigned.");
        }

        var model =
            CompressibilityModelFactory.Create(
                modelType,
                Mixture,
                interactionDatabase);

        CalculateState(model);
    }
}