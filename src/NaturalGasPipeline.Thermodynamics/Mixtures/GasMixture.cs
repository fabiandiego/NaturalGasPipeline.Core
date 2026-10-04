using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

public class GasMixture
{
    public List<GasComponent> Components { get; set; } = new();

    public double TotalMoleFraction()
    {
        return Components.Sum(
            c => c.MoleFraction);
    }

    public void ValidateComposition()
    {
        if (Components.Count == 0)
        {
            throw new InvalidOperationException(
                "Gas mixture must contain at least one component.");
        }

        foreach (var component in Components)
        {
            if (string.IsNullOrWhiteSpace(component.Name))
            {
                throw new InvalidOperationException(
                    "Gas component name cannot be empty.");
            }

            if (component.MoleFraction < 0.0 ||
                component.MoleFraction > 1.0)
            {
                throw new InvalidOperationException(
                    $"Mole fraction must be between 0 and 1. " +
                    $"Component: {component.Name}, " +
                    $"Value: {component.MoleFraction}");
            }

            double molarMass =
                component.MolarMass.KilogramsPerMole;

            if (!double.IsFinite(molarMass) || molarMass <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Molar mass must be finite and greater than zero. " +
                    $"Component: {component.Name}, " +
                    $"Value: {molarMass} kg/mol");
            }

            if (component.CriticalTemperature.Kelvins <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Critical temperature must be greater than zero. " +
                    $"Component: {component.Name}, " +
                    $"Value: {component.CriticalTemperature.Kelvins} K");
            }

            if (component.CriticalPressure.Pascals <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Critical pressure must be greater than zero. " +
                    $"Component: {component.Name}, " +
                    $"Value: {component.CriticalPressure.Pascals} Pa");
            }

            if (component.AcentricFactor <= -1.0)
            {
                throw new InvalidOperationException(
                    $"Acentric factor must be greater than -1. " +
                    $"Component: {component.Name}, " +
                    $"Value: {component.AcentricFactor}");
            }
        }

        double total = TotalMoleFraction();

        if (System.Math.Abs(total - 1.0) > 1e-10)
        {
            throw new InvalidOperationException(
                $"Mole fractions must sum to 1.0. " +
                $"Current total: {total}");
        }
    }

    /// <summary>
    /// Mixture molar mass: M = Σ yᵢ·Mᵢ.
    /// </summary>
    public MolarMass CalculateMolarMass()
    {
        return MolarMass.FromKilogramsPerMole(
            Components.Sum(
                c => c.MoleFraction * c.MolarMass.KilogramsPerMole));
    }

    public Temperature CalculateCriticalTemperature()
    {
        ValidateComposition();

        return Temperature.FromKelvins(
            Components.Sum(
                c => c.MoleFraction *
                     c.CriticalTemperature.Kelvins));
    }

    public Pressure CalculateCriticalPressure()
    {
        ValidateComposition();

        return Pressure.FromPascals(
            Components.Sum(
                c => c.MoleFraction *
                     c.CriticalPressure.Pascals));
    }


}