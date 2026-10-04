using UnitsNet;

namespace NaturalGasPipeline.Core.Thermodynamics;

/// <summary>
/// Thermodynamic state of a gas mixture at a given pressure and temperature.
///
/// Inputs (pressure, temperature, mixture) are init-only: once the state is
/// built they cannot change, so calculated results can never refer to stale
/// inputs. Every calculation checks its preconditions and throws instead of
/// returning a silently wrong value (Z = 0, M = 0, ρ = ∞, ...).
/// </summary>
public class GasState
{
    private const double R = 8.31446261815324; // J/(mol·K)

    public Pressure Pressure { get; init; }

    public Temperature Temperature { get; init; }

    public GasMixture? Mixture { get; init; }

    /// <summary>
    /// Compressibility factor. 0 means "not calculated yet".
    /// </summary>
    public double CompressibilityFactor { get; private set; }

    /// <summary>
    /// Mixture molar mass. Zero means "not calculated yet".
    /// </summary>
    public MolarMass MolarMass { get; private set; }

    public Temperature CriticalTemperature { get; private set; }

    public Pressure CriticalPressure { get; private set; }

    public double ReducedTemperature { get; private set; }

    public double ReducedPressure { get; private set; }

    public Density Density { get; private set; }

    /// <summary>
    /// Dynamic viscosity. Zero means "not calculated yet".
    /// </summary>
    public DynamicViscosity Viscosity { get; private set; }

    public void CalculateCompressibility(
        ICompressibilityModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        ValidatePressureAndTemperature();

        double z =
            model.Calculate(
                Pressure,
                Temperature);

        if (!double.IsFinite(z) || z <= 0.0)
        {
            throw new InvalidOperationException(
                $"Compressibility model {model.GetType().Name} returned " +
                $"a non-physical Z = {z} at " +
                $"P = {Pressure.Pascals} Pa, T = {Temperature.Kelvins} K.");
        }

        CompressibilityFactor = z;
    }

    public void CalculateMolarMass()
    {
        GasMixture mixture = RequireMixture();

        mixture.ValidateComposition();

        MolarMass =
            mixture.CalculateMolarMass();
    }

    /// <summary>
    /// ρ = P·M / (Z·R·T).
    /// Requires the compressibility factor to have been calculated first.
    /// </summary>
    public void CalculateDensity()
    {
        ValidatePressureAndTemperature();

        CalculateMolarMass();

        if (CompressibilityFactor <= 0.0)
        {
            throw new InvalidOperationException(
                "Compressibility factor has not been calculated. " +
                "Call CalculateCompressibility or CalculateState first.");
        }

        double p = Pressure.Pascals;
        double t = Temperature.Kelvins;
        double z = CompressibilityFactor;
        double m = MolarMass.KilogramsPerMole;

        double density =
            p * m /
            (z * R * t);

        Density =
            UnitsNet.Density.FromKilogramsPerCubicMeter(
                density);
    }

    /// <summary>
    /// Viscosity at the current state. Requires the density to have been
    /// calculated first (CalculateDensity or CalculateState).
    /// </summary>
    public void CalculateViscosity(
        IViscosityModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (Density.KilogramsPerCubicMeter <= 0.0)
        {
            throw new InvalidOperationException(
                "Density has not been calculated. " +
                "Call CalculateDensity or CalculateState first.");
        }

        DynamicViscosity viscosity =
            model.Calculate(Temperature, Density, MolarMass);

        double mu = viscosity.PascalSeconds;

        if (!double.IsFinite(mu) || mu <= 0.0)
        {
            throw new InvalidOperationException(
                $"Viscosity model {model.GetType().Name} returned " +
                $"a non-physical viscosity {mu} Pa·s.");
        }

        Viscosity = viscosity;
    }

    public void CalculateState(
        ICompressibilityModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        GasMixture mixture = RequireMixture();

        ValidatePressureAndTemperature();

        CalculateMolarMass();

        CriticalTemperature =
            mixture.CalculateCriticalTemperature();

        CriticalPressure =
            mixture.CalculateCriticalPressure();

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
        GasMixture mixture = RequireMixture();

        var model =
            CompressibilityModelFactory.Create(
                modelType,
                mixture,
                interactionDatabase);

        CalculateState(model);
    }

    private GasMixture RequireMixture()
    {
        return Mixture ??
            throw new InvalidOperationException(
                "Gas mixture has not been assigned.");
    }

    private void ValidatePressureAndTemperature()
    {
        double p = Pressure.Pascals;
        double t = Temperature.Kelvins;

        if (!double.IsFinite(p) || p <= 0.0)
        {
            throw new InvalidOperationException(
                $"Absolute pressure must be finite and greater than 0 Pa. Value: {p} Pa");
        }

        if (!double.IsFinite(t) || t <= 0.0)
        {
            throw new InvalidOperationException(
                $"Absolute temperature must be finite and greater than 0 K. Value: {t} K");
        }
    }
}
